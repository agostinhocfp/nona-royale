// Scratch NUnit stand-in runner: plain [TestFixture]/[SetUp]/[Test] only,
// which is all the EditMode suite uses. Exists because the Unity editor holds
// the project lock and the real runner cannot start. Safe to delete.
using System;
using System.Linq;
using System.Reflection;

internal static class Runner
{
    private static int Main()
    {
        var asm = Assembly.GetExecutingAssembly();
        int passed = 0, failed = 0;

        foreach (var type in asm.GetTypes().Where(t => t.GetMethods().Any(IsTest)))
        {
            int fixturePassed = 0, fixtureFailed = 0;
            foreach (var test in type.GetMethods().Where(IsTest))
            {
                object instance = null;
                try
                {
                    instance = Activator.CreateInstance(type);
                    foreach (var setup in type.GetMethods().Where(IsSetUp))
                        setup.Invoke(instance, null);
                    test.Invoke(instance, null);
                    passed++;
                    fixturePassed++;
                }
                catch (Exception e)
                {
                    failed++;
                    fixtureFailed++;
                    var inner = (e as TargetInvocationException)?.InnerException ?? e;
                    Console.WriteLine($"FAIL {type.Name}.{test.Name}: {inner.Message}");
                }
            }

            Console.WriteLine($"{type.Name}: {fixturePassed} passed, {fixtureFailed} failed");
        }

        Console.WriteLine($"passed {passed}, failed {failed}");
        return failed == 0 ? 0 : 1;
    }

    private static bool IsTest(MethodInfo m) =>
        m.GetCustomAttributes().Any(a => a.GetType().Name == "TestAttribute");

    private static bool IsSetUp(MethodInfo m) =>
        m.GetCustomAttributes().Any(a => a.GetType().Name == "SetUpAttribute");
}
