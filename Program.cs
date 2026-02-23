using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TestingEngine;
using InventoryTests;

class Program
{
    // Настройка степени параллелизма
    private const int MaxDegreeOfParallelism = 4;
    private static readonly object _consoleLock = new object();

    static async Task Main()
    {
        Console.WriteLine(">>> ENGINE V3.0 (PARALLEL EDITION) <<<\n");

        var assembly = typeof(WarehouseTests).Assembly;
        var testMethods = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<SuiteAttribute>() != null)
            .SelectMany(t => t.GetMethods()
                .Where(m => m.GetCustomAttribute<CaseAttribute>() != null)
                .Select(m => new { Type = t, Method = m }))
            .ToList();

        // 1. ПОСЛЕДОВАТЕЛЬНЫЙ ЗАПУСК
        Console.WriteLine("--- Running Sequentially ---");
        var sw = Stopwatch.StartNew();
        await RunTests(testMethods, parallel: false);
        sw.Stop();
        var seqTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"\nSequential time: {seqTime}ms\n");
        Console.WriteLine(new string('-', 30));

        // 2. ПАРАЛЛЕЛЬНЫЙ ЗАПУСК
        Console.WriteLine("--- Running Parallel ---");
        sw.Restart();
        await RunTests(testMethods, parallel: true);
        sw.Stop();
        var parTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"\nParallel time: {parTime}ms");
        Console.WriteLine($"Speedup: {(double)seqTime / parTime:F2}x");
    }

    static async Task RunTests(IEnumerable<dynamic> tests, bool parallel)
    {
        int ok = 0, fail = 0, skip = 0;
        var semaphore = new SemaphoreSlim(parallel ? MaxDegreeOfParallelism : 1);
        var tasks = new List<Task>();

        foreach (var item in tests)
        {
            var testMethod = (MethodInfo)item.Method;
            var type = (Type)item.Type;

            // Обработка CaseData (параметризованные тесты)
            var dataAttributes = testMethod.GetCustomAttributes<CaseDataAttribute>().ToList();
            var dataSets = dataAttributes.Any() ? dataAttributes.Select(d => d.Args) : new List<object[]> { null };

            foreach (var args in dataSets)
            {
                await semaphore.WaitAsync();

                var task = Task.Run(async () =>
                {
                    try
                    {
                        var result = await ExecuteSingleTest(type, testMethod, args);
                        Interlocked.Add(ref ok, result == "OK" ? 1 : 0);
                        Interlocked.Add(ref fail, result == "FAIL" ? 1 : 0);
                        Interlocked.Add(ref skip, result == "SKIP" ? 1 : 0);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                tasks.Add(task);

                if (!parallel) await task; // Если не параллельно, ждем завершения сразу
            }
        }

        await Task.WhenAll(tasks);
        Console.WriteLine($"\nFINISH -> OK: {ok} | FAIL: {fail} | SKIP: {skip}");
    }

    static async Task<string> ExecuteSingleTest(Type type, MethodInfo method, object[] args)
    {
        if (method.GetCustomAttribute<SkipAttribute>() != null)
        {
            SafeLog(method.Name, "SKIPPED", ConsoleColor.Yellow);
            return "SKIP";
        }

        // Создаем новый экземпляр класса для каждого теста (Изоляция!)
        var instance = Activator.CreateInstance(type);
        var startup = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<StartupAttribute>() != null);
        var cleanup = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<CleanupAttribute>() != null);
        var timeoutAttr = method.GetCustomAttribute<TimeoutAttribute>();

        try
        {
            startup?.Invoke(instance, null);

            object[] convertedArgs = args?.Select((a, i) =>
                Convert.ChangeType(a, method.GetParameters()[i].ParameterType)).ToArray();

            Task testTask = Task.Run(() => {
                var res = method.Invoke(instance, convertedArgs);
                if (res is Task t) return t;
                return Task.CompletedTask;
            });

            // Реализация TimeOut
            if (timeoutAttr != null)
            {
                if (await Task.WhenAny(testTask, Task.Delay(timeoutAttr.Milliseconds)) != testTask)
                    throw new EngineException($"Превышено время ожидания ({timeoutAttr.Milliseconds}ms)");
            }

            await testTask; // Дожидаемся завершения (если не вылетели по таймауту)

            SafeLog(method.Name, "PASSED", ConsoleColor.Cyan, args);
            return "OK";
        }
        catch (Exception ex)
        {
            var e = ex.InnerException ?? ex;
            string msg = e is EngineException ? $"FAIL: {e.Message}" : $"CRASH: {e.GetType().Name}";
            SafeLog(method.Name, msg, ConsoleColor.Magenta, args);
            return "FAIL";
        }
        finally
        {
            cleanup?.Invoke(instance, null);
        }
    }

    // Потокобезопасный вывод в консоль
    static void SafeLog(string testName, string status, ConsoleColor color, object[] args = null)
    {
        lock (_consoleLock)
        {
            Console.Write($"  [*] {testName}{(args != null ? $"({string.Join(",", args)})" : "")} -> ");
            Console.ForegroundColor = color;
            Console.WriteLine(status);
            Console.ResetColor();
        }
    }
}