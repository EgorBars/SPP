using ThreadPool;
using InventoryTests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TestingEngine;

class Program
{
    private static readonly object _consoleLock = new object();

    static async Task Main()
    {
        Console.WriteLine(">>> ENGINE V4.0 (CUSTOM THREAD POOL EDITION) <<<\n");

        var assembly = typeof(WarehouseTests).Assembly;
        var testMethods = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<SuiteAttribute>() != null)
            .SelectMany(t => t.GetMethods()
                .Where(m => m.GetCustomAttribute<CaseAttribute>() != null)
                .Select(m => new { Type = t, Method = m }))
            .ToList();

        // Создаем пул с динамическим масштабированием
        using (var threadPool = new DynamicThreadPool(minThreads: 2, maxThreads: 8, threadIdleTimeoutSeconds: 10))
        {
            Console.WriteLine("=== Демонстрация динамического масштабирования ===");

            // Запускаем 50+ тестов с неравномерной нагрузкой
            var stopwatch = Stopwatch.StartNew();
            int totalTasks = 0;

            // Фаза 1: Начальная нагрузка
            Console.WriteLine("\n--- Фаза 1: Постепенная подача задач ---");
            var tasks = PrepareTestTasks(testMethods, 15);
            foreach (var task in tasks)
            {
                threadPool.EnqueueTask(task);
                totalTasks++;
                await Task.Delay(100); // Пауза между задачами
            }

            await Task.Delay(2000);

            // Фаза 2: Пиковая нагрузка
            Console.WriteLine("\n--- Фаза 2: Пиковая нагрузка (быстрая подача) ---");
            var burstTasks = PrepareTestTasks(testMethods, 30);
            foreach (var task in burstTasks)
            {
                threadPool.EnqueueTask(task);
                totalTasks++;
            }

            await Task.Delay(3000);

            // Фаза 3: Интервал бездействия
            Console.WriteLine("\n--- Фаза 3: Интервал бездействия ---");
            await Task.Delay(15000);

            // Фаза 4: Единичные задачи
            Console.WriteLine("\n--- Фаза 4: Единичные задачи ---");
            var singleTasks = PrepareTestTasks(testMethods, 10);
            foreach (var task in singleTasks)
            {
                threadPool.EnqueueTask(task);
                totalTasks++;
                await Task.Delay(500);
            }

            // Ждем завершения всех задач
            Console.WriteLine($"\nВсего задач: {totalTasks}. Ожидание завершения...");
            await threadPool.WaitForCompletionAsync();

            stopwatch.Stop();

            Console.WriteLine($"\n=== ИТОГИ ===");
            Console.WriteLine($"Всего выполнено тестов: {totalTasks}");
            Console.WriteLine($"Общее время выполнения: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Среднее время на тест: {(double)stopwatch.ElapsedMilliseconds / totalTasks:F2}ms");
        }

        // Демонстрация отказоустойчивости
        await DemonstrateFaultTolerance();
    }

    private static List<ICustomTask> PrepareTestTasks(IEnumerable<dynamic> tests, int maxCount)
    {
        var tasks = new List<ICustomTask>();
        var testsList = tests.ToList();
        var random = new Random();

        for (int i = 0; i < maxCount && i < testsList.Count * 2; i++)
        {
            var test = testsList[i % testsList.Count];
            var method = (MethodInfo)test.Method;
            var type = (Type)test.Type;

            // Получаем данные для параметризованных тестов
            var dataAttributes = method.GetCustomAttributes<CaseDataAttribute>().ToList();
            var dataSets = dataAttributes.Any()
                ? dataAttributes.Select(d => d.Args)
                : new List<object[]> { null };

            foreach (var args in dataSets.Take(1)) // Берем по одному набору для разнообразия
            {
                tasks.Add(new TestTask(type, method, args));
            }
        }

        return tasks;
    }

    private static async Task DemonstrateFaultTolerance()
    {
        Console.WriteLine("\n=== Демонстрация отказоустойчивости ===");

        using (var threadPool = new DynamicThreadPool(minThreads: 2, maxThreads: 4))
        {
            // Добавляем задачу, которая "падает"
            var failingTask = new FaultyTestTask();
            threadPool.EnqueueTask(failingTask);

            // Добавляем нормальные задачи
            for (int i = 0; i < 5; i++)
            {
                threadPool.EnqueueTask(new SimpleTestTask($"NormalTask_{i}"));
            }

            await Task.Delay(5000);
            await threadPool.WaitForCompletionAsync();
        }

        Console.WriteLine("Отказоустойчивость продемонстрирована: поток восстановился после ошибки");
    }
}

// Вспомогательные классы для демонстрации
public class FaultyTestTask : ICustomTask
{
    public string Name => "FaultyTask";

    public async Task ExecuteAsync()
    {
        Console.WriteLine($"[FaultyTask] Начинаю выполнение...");
        await Task.Delay(100);
        throw new InvalidOperationException("Симуляция критической ошибки");
    }
}

public class SimpleTestTask : ICustomTask
{
    private readonly string _name;
    public string Name => _name;

    public SimpleTestTask(string name)
    {
        _name = name;
    }

    public async Task ExecuteAsync()
    {
        Console.WriteLine($"[{_name}] Выполняю работу...");
        await Task.Delay(500);
        Console.WriteLine($"[{_name}] Завершено");
    }
}