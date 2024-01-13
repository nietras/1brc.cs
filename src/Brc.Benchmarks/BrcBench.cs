using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;

namespace nietras.ComparisonBenchmarks;

[MemoryDiagnoser]
//[HardwareCounters(
//  HardwareCounter.InstructionRetired
//, HardwareCounter.TotalCycles
//, HardwareCounter.BranchInstructions
//, HardwareCounter.BranchInstructionRetired
//, HardwareCounter.BranchMispredictions
//, HardwareCounter.BranchMispredictsRetired
//, HardwareCounter.CacheMisses
//, HardwareCounter.LlcMisses
//)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory, BenchmarkLogicalGroupRule.ByParams)]
public abstract class BrcBench
{
    internal readonly string _filePath;
    internal readonly long _fileLength;
    internal readonly int _maxThreadCount;
    internal BrcSegment[] _segments = default!;
    internal BrcSegment? _eofSegment;

    protected BrcBench(int rows)
    {
        Rows = rows;
        _maxThreadCount = Environment.ProcessorCount;
        Threads = _maxThreadCount;
        ThreadsSet = new[] { Threads }; // = new[] { /*1,*/ _maxThreadCount };
        var fileName = $"measurements-{rows}.txt";
        _filePath = @$"C:\git\oss\1brc\{fileName}";
        _fileLength = Brc.GetFileLength(_filePath);
        Spec = new(fileName, _fileLength);
    }

    [ParamsSource(nameof(RowsParams))] // Attributes for params is challenging 👇
    public int Rows { get; set; }
    public IEnumerable<int> RowsParams() => new[] { Rows };

    [ParamsSource(nameof(ThreadsParams))] // Attributes for params is challenging 👇
    public int Threads { get; set; }
    public IEnumerable<int> ThreadsParams() => ThreadsSet;
    internal IEnumerable<int> ThreadsSet { get; set; }

    [ParamsSource(nameof(SpecParams))]
    public Spec Spec { get; set; }
    public IEnumerable<Spec> SpecParams() => new[] { Spec };

    [GlobalSetup]
    public void GlobalSetup()
    {
        (_segments, _eofSegment, _) = Brc.SegmentFile(_filePath, Threads);
    }
}

public abstract class RunBrcBench : BrcBench
{
    protected RunBrcBench(int rows) : base(rows)
    { }

    [Benchmark]
    public void RunMemoryMapped()
    {
        Brc.RunMemoryMapped(_filePath, Threads);
    }

    [Benchmark]
    public void RunRandomAccess()
    {
        Brc.RunRandomAccess(_filePath, Threads, 1024 * 1024);
    }
}

[BenchmarkCategory("0_100000")]
public class HundredKBrcBench : RunBrcBench
{
    public HundredKBrcBench() : base(100000) { }
}

[BenchmarkCategory("1_10000000")]
public class TenMillionBrcBench : RunBrcBench
{
    public TenMillionBrcBench() : base(10000000) { }
}

[BenchmarkCategory("2_1000000000")]
public class BillionBrcBench : RunBrcBench
{
    public BillionBrcBench() : base(1000000000) { }
}
