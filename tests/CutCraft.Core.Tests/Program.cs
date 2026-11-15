using System.Reflection;
using Xunit;

namespace CutCraft.Core.Tests;

public class Program
{
    public static int Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("========================================");
        Console.WriteLine("  CutCraft In-Process Test Runner (.NET 10)");
        Console.WriteLine("========================================");
        Console.ResetColor();

        var assembly = typeof(Program).Assembly;
        var testClasses = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Tests"))
            .OrderBy(t => t.Name)
            .ToList();

        int passed = 0;
        int failed = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var testClass in testClasses)
        {
            Console.WriteLine($"\nRunning {testClass.Name}...");
            var methods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            foreach (var method in methods)
            {
                var factAttr = method.GetCustomAttribute<FactAttribute>();
                var theoryAttr = method.GetCustomAttribute<TheoryAttribute>();
                var inlineDataAttrs = method.GetCustomAttributes<InlineDataAttribute>().ToList();

                if (factAttr != null && theoryAttr == null)
                {
                    RunSingleTest(testClass, method, null, ref passed, ref failed);
                }
                else if (theoryAttr != null)
                {
                    if (inlineDataAttrs.Count > 0)
                    {
                        foreach (var data in inlineDataAttrs)
                        {
                            RunSingleTest(testClass, method, data.GetData(method).FirstOrDefault(), ref passed, ref failed);
                        }
                    }
                    else
                    {
                        RunSingleTest(testClass, method, null, ref passed, ref failed);
                    }
                }
            }
        }

        sw.Stop();
        Console.WriteLine("\n----------------------------------------");
        if (failed == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[SUCCESS] All {passed} test runs passed in {sw.ElapsedMilliseconds}ms!");
            Console.ResetColor();
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAILED] {failed} test runs failed, {passed} passed in {sw.ElapsedMilliseconds}ms.");
            Console.ResetColor();
            return 1;
        }
    }

    private static void RunSingleTest(Type testClass, MethodInfo method, object?[]? parameters, ref int passed, ref int failed)
    {
        string paramStr = parameters != null ? $"({string.Join(", ", parameters.Select(p => p?.ToString() ?? "null"))})" : "";
        try
        {
            var instance = Activator.CreateInstance(testClass);
            var result = method.Invoke(instance, parameters);
            if (result is Task task)
            {
                task.GetAwaiter().GetResult();
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  [PASS] ");
            Console.ResetColor();
            Console.WriteLine($"{method.Name}{paramStr}");
            passed++;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("  [FAIL] ");
            Console.ResetColor();
            Console.WriteLine($"{method.Name}{paramStr}: {ex.InnerException?.Message ?? ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"    {ex.InnerException.StackTrace?.Split('\n').FirstOrDefault()}");
            }
            failed++;
        }
    }
}
