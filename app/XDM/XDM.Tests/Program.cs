// © Mayanktaker Computers & Web Development | https://mayanktaker.com

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XDM.Tests
{
    // Standalone console runner executing all test classes and test methods directly
    public class Program
    {
        public static int Main(string[] args)
        {
            var asm = typeof(Program).Assembly;
            int passed = 0, failed = 0, skipped = 0;
            var testClasses = asm.GetTypes()
                .Where(t => t.GetCustomAttributes(typeof(TestClassAttribute), true).Any())
                .ToList();

            Console.WriteLine($"Discovered {testClasses.Count} test classes in {asm.GetName().Name}.\n");

            foreach (var type in testClasses)
            {
                Console.WriteLine($"=== Running {type.Name} ===");
                object? instance = null;
                try
                {
                    instance = Activator.CreateInstance(type);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [FAIL] Failed to construct {type.Name}: {ex.Message}");
                    failed++;
                    continue;
                }

                var methods = type.GetMethods()
                    .Where(m => m.GetCustomAttributes(typeof(TestMethodAttribute), true).Any())
                    .ToList();

                foreach (var method in methods)
                {
                    try
                    {
                        method.Invoke(instance, null);
                        Console.WriteLine($"  [PASS] {method.Name}");
                        passed++;
                    }
                    catch (TargetInvocationException tie) when (tie.InnerException is AssertInconclusiveException aie)
                    {
                        Console.WriteLine($"  [SKIP] {method.Name}: {aie.Message}");
                        skipped++;
                    }
                    catch (Exception ex)
                    {
                        var inner = ex.InnerException ?? ex;
                        Console.WriteLine($"  [FAIL] {method.Name}: {inner.Message}");
                        if (inner.StackTrace != null)
                        {
                            var firstLine = inner.StackTrace.Split('\n').FirstOrDefault()?.Trim();
                            if (firstLine != null) Console.WriteLine($"         at {firstLine}");
                        }
                        failed++;
                    }
                }
                Console.WriteLine();
            }

            Console.WriteLine("==================================================");
            Console.WriteLine($"Total Tests: {passed + failed + skipped} | Passed: {passed} | Failed: {failed} | Skipped: {skipped}");
            Console.WriteLine("==================================================");

            return failed > 0 ? 1 : 0;
        }
    }
}
