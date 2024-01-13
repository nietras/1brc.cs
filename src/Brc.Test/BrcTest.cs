using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace nietras.Test;

[TestClass]
public class BrcTest
{
    public record Test(string InputFilePath, string ExpectedOutputFilePath);
    static readonly Test[] s_tests = null!;

    static BrcTest()
    {
        s_tests = GetTests();
    }

    public static IEnumerable<object[]> Tests => s_tests.Select(t => new object[] { t });

    [TestMethod]
    [DynamicData(nameof(Tests))]
    public void BrcTest_E2E_MemoryMapped_Threads_1(Test test) =>
        RunAndAssert(Brc.RunMemoryMapped, test, threadCount: 1);

    [TestMethod]
    [DynamicData(nameof(Tests))]
    public void BrcTest_E2E_MemoryMapped_Threads_8(Test test) =>
        RunAndAssert(Brc.RunMemoryMapped, test, threadCount: 8);

    [TestMethod]
    [DynamicData(nameof(Tests))]
    public void BrcTest_E2E_MemoryMapped_Threads_ProcessorCount(Test test) =>
        RunAndAssert(Brc.RunMemoryMapped, test, threadCount: Environment.ProcessorCount);

    [TestMethod]
    [DynamicData(nameof(Tests))]
    public void BrcTest_E2E_RandomAccess_Threads_1(Test test) =>
        RunAndAssert(Brc.RunRandomAccess, test, threadCount: 1);

    [TestMethod]
    [DynamicData(nameof(Tests))]
    public void BrcTest_E2E_RandomAccess_Threads_8(Test test) =>
        RunAndAssert(Brc.RunRandomAccess, test, threadCount: 8);

    [TestMethod]
    [DynamicData(nameof(Tests))]
    public void BrcTest_E2E_RandomAccess_Threads_ProcessorCount(Test test) =>
        RunAndAssert(Brc.RunRandomAccess, test, threadCount: Environment.ProcessorCount);

    static void RunAndAssert(Func<string, int, (string Result, Brc.Timings Timings)> run, Test test, int threadCount)
    {
        Trace.WriteLine($"{test} {nameof(threadCount)} = {threadCount}");

        var actual = run(test.InputFilePath, threadCount).Result;

        var expected = Encoding.UTF8.GetString(File.ReadAllBytes(test.ExpectedOutputFilePath));

        Trace.WriteLine(expected);
        Trace.WriteLine(actual);
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            var index = IndexOfNotEqual(expected, actual);
            Assert.AreEqual(expected, actual, $"{index} {expected.Length} {actual.Length} {test}");
        }
    }

    static int IndexOfNotEqual(string str1, string str2)
    {
        var minLength = Math.Min(str1.Length, str2.Length);
        for (var i = 0; i < minLength; i++)
        {
            if (str1[i] != str2[i])
            {
                return i;
            }
        }
        if (str1.Length != str2.Length)
        {
            return minLength;
        }
        return -1;
    }

    static Test[] GetTests()
    {
        var sourceDirectory = GetSourceDirectory();
        var testDirectory = Path.Combine(sourceDirectory, "../../test/");
        var inputFiles = Directory.GetFiles(testDirectory, "measurements*.txt").Select(Path.GetFullPath);
        var tests = inputFiles.Select(f => new Test(f, f.Replace(".txt", ".out"))).ToArray();
        return tests;
    }

    static string GetSourceDirectory([CallerFilePath] string sourceFilePath = "")
    {
        var sourceDirectory = Path.GetDirectoryName(sourceFilePath)!;
        return sourceDirectory;
    }
}
