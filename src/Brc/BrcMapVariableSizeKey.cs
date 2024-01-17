using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using static nietras.BrcOps;
using TAggregate = nietras.BrcAggregate;
using TSignature = nietras.BrcSignature;

namespace nietras;

// |-------------|------|-----|-------|
// |   TAggr.    | Next | Sig |  Key  |
// |-------------|------|-----|-------|
//     V * 8 b     8 b   8 b   K * 8 b
// where:
//   TAggregate = aggregate stored before e.g. BRC stats = 16 bytes
//   Next = pointer to next entry at L+H
//   Sig=L+H = Length as 1 byte, Partial hash as 7 bytes (or 4 bytes)
//   Key = stored as multiples of 8 bytes e.g. station name

[StructLayout(LayoutKind.Explicit, Size = 16)]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public struct BrcAggregate
{
    [FieldOffset(00)] public long Sum;
    [FieldOffset(08)] public int Count;
    [FieldOffset(12)] public short Min;
    [FieldOffset(14)] public short Max;

    public const uint LongSize = 2;

    internal string DebuggerDisplay => $"{DivTenRound(Min):F1}/{DivTenRound((double)Sum / Count):F1}/{DivTenRound(Max):F1} - {Count}";
}

[StructLayout(LayoutKind.Explicit, Size = 8)]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public struct BrcSignature
{
    [FieldOffset(00)] public long All;
    [FieldOffset(00)] public int KeyLength; // In bytes
    [FieldOffset(04)] public int PartialHash;

    public const uint LongSize = 1;

    internal string DebuggerDisplay => $"{nameof(KeyLength)}={KeyLength,3} {nameof(PartialHash)}={PartialHash:X8}";
}

public unsafe class BrcMapVariableSizeKey : IDisposable
{
    const uint NextLongSize = 1; // If 32-bit uses only 4 but reserve 8 for alignement
    const uint KeyMaxLongSize = (100 + 15 / 16) * 2; // 14; // 100 bytes - round up to 16 bytes 

    const nint FromSignatureLongOffsetNext = -1;
    const nint FromSignatureLongOffsetAggregate = FromSignatureLongOffsetNext - (nint)TAggregate.LongSize;
    const nint FromSignatureLongOffsetKey = 1;
    const nint SignatureLongOffset = (nint)(TAggregate.LongSize + NextLongSize);

    internal BrcPrimeInfo _primeInfo;
    internal uint _capacity;
    internal long** _buckets;
    internal long* _entries;
    internal long* _entriesCurrentEndPtr;
    internal int _count = 0;
    volatile bool _disposed = false;

    public BrcMapVariableSizeKey(uint minCapacity)
    {
        _primeInfo = BrcPrimeInfos.NextPrime(minCapacity);
        _capacity = _primeInfo.Prime;
        var bucketsByteCount = (nuint)(Unsafe.SizeOf<IntPtr>() * _capacity);
        _buckets = (long**)NativeMemory.AlignedAlloc(bucketsByteCount, (nuint)Unsafe.SizeOf<IntPtr>());
        NativeMemory.Clear(_buckets, bucketsByteCount);

        var maxLongsPerEntry = TAggregate.LongSize + NextLongSize + TSignature.LongSize + KeyMaxLongSize;
        var maxBytesPerEntry = maxLongsPerEntry * sizeof(long);
        var entriesByteCount = (nuint)(maxBytesPerEntry * _capacity);
        _entries = (long*)NativeMemory.AlignedAlloc(entriesByteCount, Brc.CacheLineSize);
#if DEBUG
        NativeMemory.Clear(_entries, entriesByteCount); // Not absolutely necessary so only DEBUG
#endif
        _entriesCurrentEndPtr = _entries + TAggregate.LongSize + NextLongSize;
    }

    public ReadOnlySpan<IntPtr> Buckets => new(_buckets, (int)_capacity);

    public ReadOnlySpan<long> Entries => new(_entries, _count);

    public int Count => _count;

    public uint Capacity => _capacity;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddOrAggregateNewKeyValue(long key, short keyLength, short value)
    {
        const int entryKeyLongSize = 1;
        Debug.Assert(keyLength <= entryKeyLongSize * sizeof(long));
        var capacity = _capacity;
        var count = _count;
        var entriesPtr = _entries;

        var hash = Hash(key);
        var bucketIndex = _primeInfo.GetIndexForHash((uint)hash);

        var bucketPtr = _buckets + bucketIndex;
        //var entriesIndex = *bucketPtr;
        var bucketEntrySignaturePtr = *bucketPtr;
#if DEBUG
        var collisions = 0;
#endif
        var newSignature = new TSignature() { KeyLength = keyLength, PartialHash = (int)hash };
        var newSignatureKey = Vector128.Create(newSignature.All, key);

        //while ((uint)entriesIndex < count)
        for (var entrySignaturePtr = bucketEntrySignaturePtr; entrySignaturePtr != null;
             entrySignaturePtr = *(long**)(entrySignaturePtr + FromSignatureLongOffsetNext))
        {
            // Compare signature (length + partial hash + key (1 long here)
            // Length must be part of comparison since map contains different lengths
            var entrySignatureKey = Vector128.Load(entrySignaturePtr);
            var equals = entrySignatureKey.Equals(newSignatureKey);
            if (equals)
            {
                var aggregatePtr = (TAggregate*)(entrySignaturePtr + FromSignatureLongOffsetAggregate);
                aggregatePtr->Count++;
                aggregatePtr->Sum += value;
                aggregatePtr->Min = Math.Min(aggregatePtr->Min, value);
                aggregatePtr->Max = Math.Max(aggregatePtr->Max, value);
#if DEBUG
                if (collisions > 1)
                {
                    Trace.WriteLine($"MAP008 COLLISIONS {collisions}");
                }
#endif
                return;
            }
#if DEBUG
            ++collisions;
#endif
        }
#if DEBUG
        if (collisions > 1)
        {
            Trace.WriteLine($"MAP008 COLLISIONS {collisions}");
        }
#endif

        if (count < capacity)
        {
            var newEntrySignaturePtr = _entriesCurrentEndPtr;

            // Aggregate
            var aggregatePtr = (TAggregate*)(newEntrySignaturePtr + FromSignatureLongOffsetAggregate);
            aggregatePtr->Sum = value;
            aggregatePtr->Count = 1;
            aggregatePtr->Min = value;
            aggregatePtr->Max = value;
            // Next (should be previous if any or new)
            *(long**)(newEntrySignaturePtr + FromSignatureLongOffsetNext) = bucketEntrySignaturePtr;
            // Signature 
            var signaturePtr = (TSignature*)(newEntrySignaturePtr);
            *signaturePtr = new TSignature() { KeyLength = keyLength, PartialHash = (int)hash };

            // Override bucket with new entry
            *bucketPtr = newEntrySignaturePtr;

            // Move end for next
            _entriesCurrentEndPtr += entryKeyLongSize + SignatureLongOffset;
            ++_count;
        }
        else
        {
            Debug.Assert(false);
        }
    }

    #region Dispose
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    // Dispose(bool disposing) executes in two distinct scenarios.
    // If disposing equals true, the method has been called directly
    // or indirectly by a user's code. Managed and unmanaged resources
    // can be disposed.
    // If disposing equals false, the method has been called by the 
    // runtime from inside the finalizer and you should not reference 
    // other objects. Only unmanaged resources can be disposed.
    protected virtual void Dispose(bool disposing)
    {
        // Dispose only if we have not already disposed.
        if (!_disposed)
        {
            // If disposing equals true, dispose all managed and unmanaged resources.
            // I.e. dispose managed resources only if true, unmanaged always.
            if (disposing)
            {
            }

            // Call the appropriate methods to clean up unmanaged resources here.
            // If disposing is false, only the following code is executed.
            NativeMemory.AlignedFree(_entries);
            _entries = null;
            NativeMemory.AlignedFree(_buckets);
            _buckets = null;
        }
        _disposed = true;
    }
    #endregion
}
