using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using Microsoft.Win32.SafeHandles;
using static nietras.BrcOps;

namespace nietras;

public sealed unsafe class BrcAccumulator(uint capacity) : IDisposable
{
    static ReadOnlySpan<byte> StationBytesMask => new byte[64]
    {
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    };
    static readonly byte* _stationBytesMaskMidPtr;
    readonly BrcMap64 _smallMap = new(capacity);
    readonly BrcMap128 _largeMap = new(capacity);

    static BrcAccumulator()
    {
        _stationBytesMaskMidPtr = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(StationBytesMask)) + StationBytesMask.Length / 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void ParseAndAggregate(byte* origin, BrcSegment segment) =>
        ParseAndAggregate(origin + segment.Offset, segment.Length);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public unsafe void ParseAndAggregate(byte* rowStart, long length)
    {
        var end = rowStart + length;

        var separators = Vector256.Create(Brc.Separator);

        var stationBytesMaskMidPtr = _stationBytesMaskMidPtr;

        while (rowStart < end)
        {
            var v0 = Vector256.Load(rowStart);
            var separatorsFound = Vector256.Equals(v0, separators);
            var separatorsMask = Vector256.ExtractMostSignificantBits(separatorsFound);
            if (separatorsMask != 0)
            {
                var separatorPosition = BitOperations.TrailingZeroCount(separatorsMask);
                var stationMask = Vector256.Load(stationBytesMaskMidPtr - separatorPosition);
                var stationNameMasked = Vector256.BitwiseAnd(v0, stationMask);
                var measurementStartPtr = rowStart + separatorPosition + 1;
                var (nextStartOffsetFromMeasurement, measurement) = ParseMeasurement(measurementStartPtr);

                _smallMap.AddOrAggregateNewKeyValue(stationNameMasked, (short)separatorPosition, measurement);

                rowStart = measurementStartPtr + nextStartOffsetFromMeasurement;

                continue;
            }
            // Handle up to 100 bytes, that is up to 3 more vector loads
            else
            {
                var v1 = Vector256.Load(rowStart + Vector256<byte>.Count * 1);
                var v2 = Vector256<byte>.Zero;
                var v3 = Vector256<byte>.Zero;
                var separatorPosition = 0;
                separatorsFound = Vector256.Equals(v1, separators);
                separatorsMask = Vector256.ExtractMostSignificantBits(separatorsFound);
                if (separatorsMask != 0)
                {
                    var localSeparatorPosition = BitOperations.TrailingZeroCount(separatorsMask);
                    var stationMask = Vector256.Load(stationBytesMaskMidPtr - localSeparatorPosition);
                    v1 = Vector256.BitwiseAnd(v1, stationMask);
                    separatorPosition = Vector256<byte>.Count * 1 + localSeparatorPosition;
                    goto ADDLARGE;
                }
                v2 = Vector256.Load(rowStart + Vector256<byte>.Count * 2);
                separatorsFound = Vector256.Equals(v2, separators);
                separatorsMask = Vector256.ExtractMostSignificantBits(separatorsFound);
                if (separatorsMask != 0)
                {
                    var localSeparatorPosition = BitOperations.TrailingZeroCount(separatorsMask);
                    var stationMask = Vector256.Load(stationBytesMaskMidPtr - localSeparatorPosition);
                    v2 = Vector256.BitwiseAnd(v2, stationMask);
                    separatorPosition = Vector256<byte>.Count * 2 + localSeparatorPosition;
                    goto ADDLARGE;
                }
                v3 = Vector256.Load(rowStart + Vector256<byte>.Count * 3);
                separatorsFound = Vector256.Equals(v3, separators);
                separatorsMask = Vector256.ExtractMostSignificantBits(separatorsFound);
                if (separatorsMask != 0)
                {
                    var localSeparatorPosition = BitOperations.TrailingZeroCount(separatorsMask);
                    var stationMask = Vector256.Load(stationBytesMaskMidPtr - localSeparatorPosition);
                    v3 = Vector256.BitwiseAnd(v3, stationMask);
                    separatorPosition = Vector256<byte>.Count * 3 + localSeparatorPosition;
                    goto ADDLARGE;
                }
            ADDLARGE:
                var measurementStartPtr = rowStart + separatorPosition + 1;
                var (nextStartOffsetFromMeasurement, measurement) = ParseMeasurement(measurementStartPtr);

                _largeMap.AddOrAggregateNewKeyValue(v0, v1, v2, v3, (short)separatorPosition, measurement);

                rowStart = measurementStartPtr + nextStartOffsetFromMeasurement;
            }

        }
    }

    [SkipLocalsInit]
    public unsafe void ParseAndAggregate(string filePath, int bufferSize, BrcSegment segment)
    {
        using var file = Brc.OpenFileHandle(filePath, FileOptions.SequentialScan);
        ParseAndAggregate(file, bufferSize, segment);
    }

    // RandomAccess version
    [SkipLocalsInit]
    public unsafe void ParseAndAggregate(SafeFileHandle file, int bufferSize, BrcSegment segment)
    {
        var bufferLength = (int)Math.Min(bufferSize, segment.Length);
        var bufferPtr = stackalloc byte[bufferLength];
        var buffer = new Span<byte>(bufferPtr, bufferLength - Brc.VectorSizeMax);
        var bufferOffset = 0;
        var offset = segment.Offset;
        var length = segment.Length;
        var end = offset + length;
        var read = 0;
        while (offset < end &&
               (read = RandomAccess.Read(file, buffer.Slice(bufferOffset, (int)Math.Min(end - offset, buffer.Length - bufferOffset)), offset)) > 0)
        {
            var dataLength = read + bufferOffset;
            var dataSpan = buffer.Slice(0, dataLength);
            var lineFeed = dataSpan.LastIndexOf(Brc.LineFeed);
            var parseLength = lineFeed + 1;

            ParseAndAggregate(bufferPtr, parseLength);

            // Copy remaining to start of buffer
            var remainingLength = dataLength - parseLength;
            if (remainingLength > 0)
            {
                var remainingSpan = dataSpan.Slice(parseLength, remainingLength);
                remainingSpan.CopyTo(buffer);
                bufferOffset = remainingLength;
            }
            else
            {
                bufferOffset = 0;
            }

            offset += read;
        }
    }

    public void AddOrAggregate(BrcAccumulator other)
    {
        _smallMap.AddOrAggregateMap(other._smallMap);
        _largeMap.AddOrAggregateMap(other._largeMap);
    }

    public void AppendStats(StringBuilder sb)
    {
        var sortedNameToStats = new SortedDictionary<string, (short min, long sum, int count, short max)>(StringComparer.Ordinal);
        foreach (var entry in _smallMap.Entries)
        {
            sortedNameToStats.Add(entry.Name, (entry.Min, entry.Sum, entry.Count, entry.Max));
        }
        foreach (var entry in _largeMap.Entries)
        {
            sortedNameToStats.Add(entry.Name, (entry.Min, entry.Sum, entry.Count, entry.Max));
        }

        // TEST: For printing length distribution
        //var list = sortedNameToStats.GroupBy(e => e.Key.Length).ToList();
        //list.Sort((x, y) => x.Key.CompareTo(y.Key));
        //list.ForEach(g => sb.AppendLine($"{g.Key,3} = {g.Count(),5}"));

        sb.Append('{');
        var notFirst = false;
        foreach (var e in sortedNameToStats)
        {
            if (notFirst) { sb.Append(", "); } else { notFirst = true; }
            var (min, sum, count, max) = e.Value;
            sb.Append($"{e.Key}={DivTenRound(min):F1}/{DivTenRound((double)sum / count):F1}/{DivTenRound(max):F1}");
        }
        sb.Append('}');
        sb.Append('\n');
    }

    void DisposeManagedResources()
    {
        _smallMap.Dispose();
        _largeMap.Dispose();
    }

    #region Dispose
    public void Dispose()
    {
        Dispose(true);
    }

    // Dispose(bool disposing) executes in two distinct scenarios.
    // If disposing equals true, the method has been called directly
    // or indirectly by a user's code. Managed and unmanaged resources
    // can be disposed.
    // If disposing equals false, the method has been called by the 
    // runtime from inside the finalizer and you should not reference 
    // other objects. Only unmanaged resources can be disposed.
    void Dispose(bool disposing)
    {
        // Dispose only if we have not already disposed.
        if (!m_disposed)
        {
            // If disposing equals true, dispose all managed and unmanaged resources.
            // I.e. dispose managed resources only if true, unmanaged always.
            if (disposing)
            {
                DisposeManagedResources();
            }

            // Call the appropriate methods to clean up unmanaged resources here.
            // If disposing is false, only the following code is executed.
        }
        m_disposed = true;
    }

    volatile bool m_disposed = false;
    #endregion
}
