using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TestingEngine;
using InventoryTests;

class Program
{
    static async Task Main()
    {
        Console.WriteLine(">>> ENGINE V2.0 STARTING <<<\n");
        var assembly = typeof(WarehouseTests).Assembly;
        var suites = assembly.GetTypes().Where(t => t.GetCustomAttribute<SuiteAttribute>() != null);

        int ok = 0, fail = 0, skip = 0;

        foreach (var type in suites)
        {
            Console.WriteLine($"[SUITE]: {type.Name}");
            var instance = Activator.CreateInstance(type);
            var startup = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<StartupAttribute>() != null);
            var cleanup = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<CleanupAttribute>() != null);
            var cases = type.GetMethods()
    .Where(m => m.GetCustomAttribute<CaseAttribute>() != null)
    .OrderBy(m => m.GetCustomAttribute<CaseAttribute>().Order);

            foreach (var test in cases)
            {
                if (test.GetCustomAttribute<SkipAttribute>() != null)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  [-] {test.Name} -> SKIPPED");
                    skip++; Console.ResetColor(); continue;
                }

                var data = test.GetCustomAttributes<CaseDataAttribute>().Select(d => d.Args).DefaultIfEmpty(null);
                foreach (var args in data)
                {
                    Console.Write($"  [*] Running: {test.Name}{(args != null ? $"({string.Join(",", args)})" : "")} ... ");
                    try
                    {
                        startup?.Invoke(instance, null);
                        object[] converted = args?.Select((a, i) => Convert.ChangeType(a, test.GetParameters()[i].ParameterType)).ToArray();
                        var res = test.Invoke(instance, converted);
                        if (res is Task t) await t;
                        Console.ForegroundColor = ConsoleColor.Cyan; Console.WriteLine("PASSED"); ok++;
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        var e = ex.InnerException ?? ex;
                        Console.WriteLine(e is EngineException ? $"FAIL: {e.Message}" : $"CRASH: {e.GetType().Name}");
                        fail++;
                    }
                    finally { cleanup?.Invoke(instance, null); Console.ResetColor(); }
                }
            }
        }
        Console.WriteLine($"\nTOTAL -> OK: {ok} | FAILED: {fail} | SKIPPED: {skip}");
    }
}
