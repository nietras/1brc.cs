using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Attributes;

namespace nietras.ComparisonBenchmarks;

[BenchmarkCategory("0")]
public class RandomAccessReadFileOnlyBench : BrcBench
{
    public RandomAccessReadFileOnlyBench() : base(1_000_000_000)
    { ThreadsSet = [8, 16, 24, 32]; }

    [Params(32 * 1024, 128 * 1024, 1024 * 1024)]
    public int Buffer { get; set; }

    [Benchmark]
    public void ReadFileRandomAccess()
    {
        if (Threads == 1)
        {
            ReadFileOnlyByRandomAccessSingleThread(_filePath, Buffer);
        }
        else
        {
            ReadFileOnlyByRandomAccessMultiplyThreads(_filePath, _segments, Buffer);
        }
    }

    [SkipLocalsInit]
    public static void ReadFileOnlyByRandomAccessMultiplyThreads(string filePath, BrcSegment[] segments, int bufferSize)
    {
        var threads = (new Thread[segments.Length]).AsSpan();
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var thread = new Thread(_ =>
            {
                // Using file handle per thread since perf otherwise poor
                using var file = File.OpenHandle(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                Span<byte> buffer = stackalloc byte[bufferSize];
                var offset = segment.Offset;
                var length = segment.Length;
                var end = offset + length;
                long read = 0;
                while (offset < end &&
                       (read = RandomAccess.Read(file, buffer.Slice(0, (int)Math.Min(end - offset, buffer.Length)), offset)) > 0)
                { offset += read; }

            });
            threads[i] = thread;
            thread.Start();
        }
        foreach (var t in threads) { t.Join(); }
    }

    [SkipLocalsInit]
    public static void ReadFileOnlyByRandomAccessSingleThread(string filePath, int bufferSize)
    {
        using var file = Brc.OpenFileHandle(filePath);
        Span<byte> buffer = stackalloc byte[bufferSize];
        long offset = 0;
        long read = 0;
        while ((read = RandomAccess.Read(file, buffer, offset)) > 0) { offset += read; }
    }
}

