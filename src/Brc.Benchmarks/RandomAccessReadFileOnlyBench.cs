using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Attributes;

namespace nietras.ComparisonBenchmarks;

[BenchmarkCategory("0")]
public class RandomAccessReadFileOnlyBench : BrcBench
{
    public RandomAccessReadFileOnlyBench() : base(1_000_000_000)
    { ThreadsSet = [8]; } // 8, 16, 24, 32]; }

    [Params(/*32 * 1024, 128 * 1024, */1024 * 1024)]
    public int Buffer { get; set; } = 1024 * 1024;

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

    [Benchmark]
    public void ReadFileRandomAccessInterlocked()
    {
        if (Threads == 1)
        {
            ReadFileOnlyByRandomAccessSingleThread(_filePath, Buffer);
        }
        else
        {
            ReadFileOnlyByRandomAccessMultiplyThreadsInterlocked(_filePath, _segments.Length, Buffer, _fileLength);
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

    // Sequential read of file using RandomAccess.Read with Interlocked.Add, not faster than splitting file into segments
    [SkipLocalsInit]
    public void ReadFileOnlyByRandomAccessMultiplyThreadsInterlocked(string filePath, int threadCount, int bufferSize, long fileLength)
    {
        long offset = 0;
        var threads = (new Thread[threadCount]).AsSpan();
        for (var i = 0; i < threadCount; i++)
        {
            var thread = new Thread(_ =>
            {
                // Using file handle per thread since perf otherwise poor
                using var file = File.OpenHandle(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                //Console.WriteLine($"// T{Environment.CurrentManagedThreadId,2}");

                Span<byte> buffer = stackalloc byte[bufferSize];
                do
                {
                    var maybeEnd = Interlocked.Add(ref offset, bufferSize);
                    var fileOffset = maybeEnd - bufferSize;
                    if (fileOffset >= fileLength) { break; }

                    var fileReadEnd = Math.Min(maybeEnd, fileLength);
                    var readLength = (int)(fileReadEnd - fileOffset);

                    //Trace.WriteLine($"T{Environment.CurrentManagedThreadId,2} [{fileOffset,11}..{fileReadEnd,11}] {readLength,9}");

                    var read = 0;
                    while (read < readLength && (read = RandomAccess.Read(file, buffer.Slice(read, readLength - read), fileOffset + read)) > 0)
                    {
                        //Trace.WriteLine($"{Environment.CurrentManagedThreadId,2} {fileOffset,9} {fileOffset + read,9} {read,9} {readLength - read,9}");
                    }
                } while (true);
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

