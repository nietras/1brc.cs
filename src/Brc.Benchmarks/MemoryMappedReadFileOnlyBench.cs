using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Attributes;

namespace nietras.ComparisonBenchmarks;

[BenchmarkCategory("0")]
public class MemoryMappedReadFileOnlyBench : BrcBench
{
    public MemoryMappedReadFileOnlyBench() : base(1_000_000_000)
    { ThreadsSet = [8, 16, 24, 32]; }

    [Benchmark]
    public void ReadFileMemoryMap()
    {
        if (Threads == 1)
        {
            ReadFileOnlyByMemoryMapSingleThread(_filePath);
        }
        else
        {
            ReadFileOnlyByMemoryMapMultipleThreads(_filePath, _segments);
        }
    }


    [SkipLocalsInit]
    public static unsafe void ReadFileOnlyByMemoryMapMultipleThreads(string filePath, BrcSegment[] segments)
    {
        using var file = Brc.OpenFileHandle(filePath);
        var fileLength = RandomAccess.GetLength(file);
        using var memoryMappedFile = MemoryMappedFile.CreateFromFile(file, Path.GetFileName(filePath),
            fileLength, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
        using var viewAccessor = memoryMappedFile.CreateViewAccessor(0, fileLength, MemoryMappedFileAccess.Read);
        var memoryMappedViewHandle = viewAccessor.SafeMemoryMappedViewHandle;
        byte* ptr = null;
        memoryMappedViewHandle.AcquirePointer(ref ptr);


        var threads = (new Thread[segments.Length]).AsSpan();
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var thread = new Thread(_ =>
            {
                var offset = segment.Offset;
                var length = segment.Length;
                var end = offset + length;
                // Step wise read from mmap (should load each page into memory)
                for (var i = offset; i < end; i += 1024)
                {
                    var b = ptr[i];
                }

            });
            threads[i] = thread;
            thread.Start();
        }
        foreach (var t in threads) { t.Join(); }
    }

    [SkipLocalsInit]
    public static unsafe void ReadFileOnlyByMemoryMapSingleThread(string filePath)
    {
        using var file = Brc.OpenFileHandle(filePath);
        var fileLength = RandomAccess.GetLength(file);
        using var memoryMappedFile = MemoryMappedFile.CreateFromFile(file, Path.GetFileName(filePath),
            fileLength, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
        using var viewAccessor = memoryMappedFile.CreateViewAccessor(0, fileLength, MemoryMappedFileAccess.Read);
        var memoryMappedViewHandle = viewAccessor.SafeMemoryMappedViewHandle;
        byte* ptr = null;
        memoryMappedViewHandle.AcquirePointer(ref ptr);

        // Step wise read from mmap (should load each page into memory)
        for (long i = 0; i < fileLength; i += 1024)
        {
            var b = ptr[i];
        }
    }
}

