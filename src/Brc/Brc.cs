using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace nietras;

public static class Brc
{
    public const int CacheLineSize = 64;
    public const int VectorSizeMax = CacheLineSize;
    public const int SegmentLengthThreshold = VectorSizeMax * 4;
    public const int SegmentLengthMin = 32 * 1024 - CacheLineSize;
    public const int SegmentNewLineSearchLength = 256; // Must guarantee at least one line feed found
    public const uint StationCountMax = 10000;
    public const byte Separator = (byte)';';
    public const byte LineFeed = (byte)'\n';

    static readonly Func<long, double> ToMs = t => (1000.0 * t) / Stopwatch.Frequency;

    public record struct Timings(double OpenFileHandle_ms, double Segment_ms,
        double MaybeMemoryMap_ms, double ParseAndAggregate_ms, double Dispose_ms,
        double Eof_ms, double ToString_ms);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public unsafe static (string Result, Timings Timings) RunMemoryMapped(string filePath, int threadCount)
    {
        var t0 = Stopwatch.GetTimestamp();
        using var file = OpenFileHandle(filePath);

        var t1 = Stopwatch.GetTimestamp();
        var (segments, eofSegment, fileLength) = SegmentFile(file, threadCount);

        var t2 = Stopwatch.GetTimestamp();
        using var memoryMappedFile = MemoryMappedFile.CreateFromFile(file, mapName: null,
            fileLength, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
        using var viewAccessor = memoryMappedFile.CreateViewAccessor(0, fileLength, MemoryMappedFileAccess.Read);
        using var memoryMappedViewHandle = viewAccessor.SafeMemoryMappedViewHandle;
        byte* memoryMapPointer = null;
        memoryMappedViewHandle.AcquirePointer(ref memoryMapPointer);

        var t3 = Stopwatch.GetTimestamp();
        using var mainAccumulator = new BrcAccumulator(StationCountMax);
        var t4 = Stopwatch.GetTimestamp();
        var t5 = Stopwatch.GetTimestamp();
        if (segments.Length == 0)
        {
            // File short so handled by EOF segment
        }
        else if (segments.Length == 1)
        {
            var segment = segments[0];
            mainAccumulator.ParseAndAggregate(memoryMapPointer, segment);
            t4 = Stopwatch.GetTimestamp();
        }
        else
        {
            var accumulators = (new BrcAccumulator[segments.Length]).AsSpan();
            var threads = (new Thread[segments.Length]).AsSpan();

            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var accumulator = i == 0 ? mainAccumulator : new BrcAccumulator(StationCountMax);
                var thread = new Thread(_ => accumulator.ParseAndAggregate(memoryMapPointer, segment));
                thread.Priority = ThreadPriority.Highest;
                accumulators[i] = accumulator;
                threads[i] = thread;
                thread.Start();
            }

            // Wait for first/main thread to finish
            threads[0].Join();

            // Merge one at a time as remaining finish
            for (var i = 1; i < threads.Length; i++)
            {
                threads[i].Join();
                var accumulator = accumulators[i];
                mainAccumulator.AddOrAggregate(accumulator);
            }

            t4 = Stopwatch.GetTimestamp();

            // Dispose all but main accumulator
            for (var i = 1; i < accumulators.Length; i++)
            {
                accumulators[i].Dispose();
            }

        }
        t5 = Stopwatch.GetTimestamp();

        // Handle EOF segment
        if (eofSegment is not null)
        {
            // Copy EOF segment to span with padding after last to allow vector loads past that
            var segment = eofSegment.Value;
            var eofSpan = new Span<byte>(memoryMapPointer + segment.Offset, (int)segment.Length);
            var eofWithPaddingLength = eofSpan.Length + VectorSizeMax;
            var eofWithPaddingBytes = stackalloc byte[eofWithPaddingLength];
            var eofWithPaddingSpanJustData = new Span<byte>(eofWithPaddingBytes, eofSpan.Length);

            eofSpan.CopyTo(eofWithPaddingSpanJustData);

            mainAccumulator.ParseAndAggregate(eofWithPaddingBytes, segment.Length);
        }

        var t6 = Stopwatch.GetTimestamp();

        var sb = new StringBuilder(16 * 1024);
        mainAccumulator.AppendStats(sb);
        var result = sb.ToString();

        var t7 = Stopwatch.GetTimestamp();

        var timings = new Timings(ToMs(t1 - t0), ToMs(t2 - t1), ToMs(t3 - t2), ToMs(t4 - t3), ToMs(t5 - t4), ToMs(t6 - t5), ToMs(t7 - t6));

        return (result, timings);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe static (string Result, Timings Timings) RunRandomAccess(string filePath, int threadCount) =>
        RunRandomAccess(filePath, threadCount, bufferSize: 128 * 1024);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public unsafe static (string Result, Timings Timings) RunRandomAccess(string filePath, int threadCount, int bufferSize)
    {
        var t0 = Stopwatch.GetTimestamp();
        using var file = OpenFileHandle(filePath);

        var t1 = Stopwatch.GetTimestamp();
        var (segments, eofSegment, fileLength) = SegmentFile(file, threadCount);

        var t2 = Stopwatch.GetTimestamp();

        var t3 = Stopwatch.GetTimestamp();
        using var mainAccumulator = new BrcAccumulator(StationCountMax);
        var t4 = Stopwatch.GetTimestamp();
        var t5 = Stopwatch.GetTimestamp();
        if (segments.Length == 0)
        {
            // File short so handled by EOF segment
        }
        else if (segments.Length == 1)
        {
            var segment = segments[0];
            mainAccumulator.ParseAndAggregate(file, bufferSize, segment);
            t4 = Stopwatch.GetTimestamp();
        }
        else
        {
            var accumulators = (new BrcAccumulator[segments.Length]).AsSpan();
            var threads = (new Thread[segments.Length]).AsSpan();

            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var accumulator = i == 0 ? mainAccumulator : new BrcAccumulator(StationCountMax);
                var thread = new Thread(_ => accumulator.ParseAndAggregate(filePath, bufferSize, segment));
                thread.Priority = ThreadPriority.Highest;
                accumulators[i] = accumulator;
                threads[i] = thread;
                thread.Start();
            }

            // Wait for first/main thread to finish
            threads[0].Join();

            // Merge one at a time as remaining finish
            for (var i = 1; i < threads.Length; i++)
            {
                threads[i].Join();
                var accumulator = accumulators[i];
                mainAccumulator.AddOrAggregate(accumulator);
            }

            t4 = Stopwatch.GetTimestamp();

            // Dispose all but main accumulator
            for (var i = 1; i < accumulators.Length; i++)
            {
                accumulators[i].Dispose();
            }

        }
        t5 = Stopwatch.GetTimestamp();

        // Handle EOF segment
        if (eofSegment is not null)
        {
            // Copy EOF segment to span with padding after last to allow vector loads past that
            var segment = eofSegment.Value;
            var eofBytes = stackalloc byte[(int)segment.Length + VectorSizeMax];
            var read = RandomAccess.Read(file, new Span<byte>(eofBytes, (int)segment.Length), segment.Offset);
            var eofSpan = new Span<byte>(eofBytes, read);
            Debug.Assert(read == segment.Length);

            mainAccumulator.ParseAndAggregate(eofBytes, segment.Length);
        }

        var t6 = Stopwatch.GetTimestamp();

        var sb = new StringBuilder(16 * 1024);
        mainAccumulator.AppendStats(sb);
        var result = sb.ToString();

        var t7 = Stopwatch.GetTimestamp();

        var timings = new Timings(ToMs(t1 - t0), ToMs(t2 - t1), ToMs(t3 - t2), ToMs(t4 - t3), ToMs(t5 - t4), ToMs(t6 - t5), ToMs(t7 - t6));

        return (result, timings);
    }

    public static (BrcSegment[] Segments, BrcSegment? EndOfFileSegment, long FileLength) SegmentFile(
        string filePath, int threadCount)
    {
        using var file = OpenFileHandle(filePath);
        return SegmentFile(file, threadCount);
    }

    public static (BrcSegment[] Segments, BrcSegment? EndOfFileSegment, long FileLength) SegmentFile(SafeFileHandle file, int threadCount)
    {
        var fileLength = RandomAccess.GetLength(file);

        var segments = FindSegments(fileLength, threadCount);

        var (adjustedSegments, endOfFileSegment) = AdjustSegmentsToLineEndsAndLeaveOutLastSegment(
            file, fileLength, segments);

        return (adjustedSegments, endOfFileSegment, fileLength);
    }

    public static long GetFileLength(string filePath)
    {
        using var file = OpenFileHandle(filePath);
        return RandomAccess.GetLength(file);
    }

    public static SafeFileHandle OpenFileHandle(string filePath, FileOptions fileOptions = FileOptions.None) =>
        File.OpenHandle(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, fileOptions);

    public static BrcSegment[] FindSegments(long fileLength, int threadCount)
    {
        var segmentLength = fileLength / threadCount;
        // Ensure minimum segment length
        segmentLength = Math.Min(fileLength, Math.Max(SegmentLengthMin, segmentLength));
        // If too short return return no segments, so file handled by EOF segment
        if (segmentLength < SegmentLengthThreshold) { return []; }

        var segmentCount = fileLength / segmentLength;
        var segments = new BrcSegment[segmentCount];
        for (var i = 0; i < segmentCount; ++i)
        {
            segments[i] = new(i * segmentLength, segmentLength);
        }
        // Adjust last segment to end of file
        ref var lastSegment = ref segments[^1];
        lastSegment.Length += fileLength - lastSegment.End;
        return segments;
    }

    // Adjust segments to where line endings are leave out last rows at end of
    // file so always sure can read at least vector size/64 bytes.
    [SkipLocalsInit]
    public unsafe static (BrcSegment[] Segments, BrcSegment? EndOfFileSegment) AdjustSegmentsToLineEndsAndLeaveOutLastSegment(
        SafeFileHandle file, long fileLength, BrcSegment[] segments)
    {
        if (segments.Length == 0)
        {
            return (segments, new(0, fileLength));
        }
        if (segments.Length == 1)
        {
            var eofSegment = FindEndOfFileSegment(file, fileLength, ref segments[^1]);
            return (segments, eofSegment);
        }

        var adjustedSegments = new BrcSegment[segments.Length];
        var lastIndex = segments.Length - 1;

        Span<byte> span = stackalloc byte[SegmentNewLineSearchLength];
        var previousEnd = 0L;
        for (var i = 0; i < lastIndex; i++)
        {
            var rawSegment = segments[i];
            var position = rawSegment.End;
            int read;
            while ((read = RandomAccess.Read(file, span, position)) > 0)
            {
                var index = span.IndexOf(LineFeed);
                if (index >= 0)
                {
                    var adjustedSegmentEnd = position + index + 1;
                    var adjustedLength = adjustedSegmentEnd - previousEnd;
                    adjustedSegments[i] = new(previousEnd, adjustedLength);
                    previousEnd = adjustedSegmentEnd;
                    break;
                }
                position += read;
            };
        }

        // Last segment
        ref var lastSegment = ref adjustedSegments[lastIndex];
        lastSegment = new(previousEnd, fileLength - previousEnd);
        var endOfFileSegment = FindEndOfFileSegment(file, fileLength, ref lastSegment);

        return (adjustedSegments, endOfFileSegment);
    }

    [SkipLocalsInit]
    unsafe static BrcSegment FindEndOfFileSegment(SafeFileHandle file, long fileLength, ref BrcSegment lastSegment)
    {
        // Search for last segment for line feed that is at least max vector
        // size from end of file
        Span<byte> span = stackalloc byte[SegmentNewLineSearchLength];
        var lastSearchPosition = Math.Max(0, fileLength - SegmentNewLineSearchLength);
        int lastRead;
        while ((lastRead = RandomAccess.Read(file, span, lastSearchPosition)) > 0)
        {
            var index = span.IndexOf(LineFeed);
            if (index >= 0)
            {
                var adjustedLength = (lastSearchPosition + index + 1) - lastSegment.Offset;
                lastSegment = new(lastSegment.Offset, adjustedLength);
                break;
            }
            lastSearchPosition += lastRead;
        };
        var endOfFileSegment = new BrcSegment(lastSegment.End, fileLength - lastSegment.End);
        Debug.Assert(endOfFileSegment.Length > VectorSizeMax);
        return endOfFileSegment;
    }
}
