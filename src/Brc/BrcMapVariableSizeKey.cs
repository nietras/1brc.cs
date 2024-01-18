using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using static nietras.BrcOps;
using TAggregate = nietras.BrcAggregate;
using TSignature = nietras.BrcSignature;

namespace nietras;

unsafe interface IBrcMapVariableSizeKeyHelper<TKey, TSignature, TSignatureKey>
{
    static abstract uint Hash(in TKey key);

    static abstract TSignature CreateSignature(short keyLength, uint partialHash);

    static abstract TSignatureKey MaybeCombineSignatureKey(in TSignature signature, in TKey key);

    static abstract bool AreSignatureKeyEqual(in TSignatureKey newSignatureKey, in TKey key, long* entrySignaturePtr);

    static abstract void StoreKey(in TKey key, long* destination);
}

unsafe abstract class BrcMapVariableSizeKeyHelperLong
    : IBrcMapVariableSizeKeyHelper<long, long, Vector128<long>>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Hash(in long key) => (uint)BrcOps.Hash(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long CreateSignature(short keyLength, uint hash) =>
        keyLength;
    // Don't  need hash in signature
    //new ((long)keyLength | ((long)hash << 32));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<long> MaybeCombineSignatureKey(in long signature, in long key) =>
        Vector128.Create(signature, key);
    //public static Vector128<long> MaybeCombineSignatureKey(in TSignature signature, in long key) =>
    //    Vector128.Create(signature.All, key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreSignatureKeyEqual(in Vector128<long> signatureKey, in long key, long* entrySignaturePtr)
    {
        var entrySignatureKey = Vector128.Load(entrySignaturePtr);
        var equals = entrySignatureKey.Equals(signatureKey);
        return equals;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void StoreKey(in long key, long* destination)
    {
        *destination = key;
    }
}

unsafe abstract class BrcMapVariableSizeKeyHelperVector256
    : IBrcMapVariableSizeKeyHelper<Vector256<byte>, long, long>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Hash(in Vector256<byte> key) => (uint)BrcOps.Hash(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long CreateSignature(short keyLength, uint hash) => keyLength;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long MaybeCombineSignatureKey(in long signature, in Vector256<byte> key) => signature;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreSignatureKeyEqual(in long signature, in Vector256<byte> key, long* entrySignaturePtr)
    {
        var entrySignature = *entrySignaturePtr;
        var entryKey = Vector256.Load((byte*)(entrySignaturePtr + 1));
        var signatureBitEquals = signature ^ entrySignature;
        var keyBitEquals = key ^ entryKey;
        var keyBitMaskEquals = keyBitEquals.ExtractMostSignificantBits();
        var equalsBitMask = signatureBitEquals | keyBitMaskEquals;
        return equalsBitMask == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void StoreKey(in Vector256<byte> key, long* destination)
    {
        Vector256.Store(key, (byte*)destination);
    }
}


// |-------------|------|-----|-------|
// |   TAggr.    | Next | Sig |  Key  |
// |-------------|------|-----|-------|
//     A * 8 b     8 b   8 b   K * 8 b
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

    internal string DebuggerDisplay => $"{DivTenRound(Min):F1}/{DivTenRound((double)Sum / Count):F1}/{DivTenRound(Max):F1} | {Count}";
}

[StructLayout(LayoutKind.Explicit, Size = 8)]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public struct BrcSignature
{
    public BrcSignature(long all)
    {
        All = all;
    }

    [FieldOffset(00)] public long All;
    [FieldOffset(00)] public int KeyLength; // In bytes
    [FieldOffset(04)] public int PartialHash;

    public const uint LongSize = 1;

    internal string DebuggerDisplay => $"{nameof(KeyLength)}={KeyLength,3} {nameof(PartialHash)}={PartialHash:X8}";
}

public record struct BrcEnumerateEntry(string Name, TAggregate Aggregate);

public unsafe class BrcMapVariableSizeKey : IDisposable
{
    const uint NextLongSize = 1; // If 32-bit uses only 4 but reserve 8 for alignement
    const uint KeyMaxLongSize = (100 + 15 / 16) * 2; // 14; // 100 bytes - round up to 16 bytes 

    const nint FromSignatureLongOffsetNext = -1;
    const nint FromSignatureLongOffsetAggregate = FromSignatureLongOffsetNext - (nint)TAggregate.LongSize;
    const nint FromSignatureLongOffsetKey = (nint)TSignature.LongSize;
    const nint SignatureLongOffset = (nint)(TAggregate.LongSize + NextLongSize);

    internal BrcPrimeInfo _primeInfo;
    internal uint _capacity;
    internal long** _buckets;
    internal long* _entries;
    internal long* _entriesCurrentEndPtr;
    internal int _count = 0;
#if DEBUG
    internal int _totalCollisions = 0;
#endif

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

    public IntPtr EntriesIntPtr => new(_entries);

    public int Count => _count;

    public uint Capacity => _capacity;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddOrAggregateNewKeyValueLong(long key, short keyLength, short value) =>
        AddOrAggregateNewKeyValueGeneric<long, long, Vector128<long>, BrcMapVariableSizeKeyHelperLong>(key, keyLength, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddOrAggregateNewKeyValueVector256(Vector256<byte> key, short keyLength, short value) =>
        AddOrAggregateNewKeyValueGeneric<Vector256<byte>, long, long, BrcMapVariableSizeKeyHelperVector256>(key, keyLength, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void AddOrAggregateNewKeyValueGeneric<TKey, TSignature, TKeySignature, TMapHelper>(in TKey key, short keyLength, short value)
        where TMapHelper : IBrcMapVariableSizeKeyHelper<TKey, TSignature, TKeySignature>
        where TKey : unmanaged
        where TSignature : unmanaged
        where TKeySignature : unmanaged
    {
        const int entryKeyLongSize = 1;
        Debug.Assert(keyLength <= entryKeyLongSize * sizeof(long));
        var capacity = _capacity;
        var count = _count;
        var entriesPtr = _entries;

        var hash = TMapHelper.Hash(key);
        var bucketIndex = _primeInfo.GetIndexForHash(hash);

        var bucketPtr = _buckets + bucketIndex;
        var bucketEntrySignaturePtr = *bucketPtr;
#if DEBUG
        var collisions = 0;
#endif
        var newSignature = TMapHelper.CreateSignature(keyLength, hash);

        var newSignatureKey = TMapHelper.MaybeCombineSignatureKey(newSignature, key);

        for (var entrySignaturePtr = bucketEntrySignaturePtr; entrySignaturePtr != null;
             entrySignaturePtr = *(long**)(entrySignaturePtr + FromSignatureLongOffsetNext))
        {
            var equals = TMapHelper.AreSignatureKeyEqual(newSignatureKey, key, entrySignaturePtr);
            if (equals)
            {
                var aggregatePtr = (TAggregate*)(entrySignaturePtr + FromSignatureLongOffsetAggregate);
                aggregatePtr->Sum += value;
                aggregatePtr->Count++;
                aggregatePtr->Min = Math.Min(aggregatePtr->Min, value);
                aggregatePtr->Max = Math.Max(aggregatePtr->Max, value);
#if DEBUG
                _totalCollisions += collisions;
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
        _totalCollisions += collisions;
        if (collisions > 1)
        {
            Trace.WriteLine($"MAP008 COLLISIONS {collisions}");
        }
#endif

        Debug.Assert(count < capacity);
        //if (count < capacity)
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
            *signaturePtr = TMapHelper.CreateSignature(keyLength, hash);
            // Key
            TMapHelper.StoreKey(key, newEntrySignaturePtr + FromSignatureLongOffsetKey);

            // Override bucket with new entry
            *bucketPtr = newEntrySignaturePtr;

            // Move end for next
            _entriesCurrentEndPtr += entryKeyLongSize + SignatureLongOffset;
            ++_count;
        }
    }

    public IReadOnlyList<BrcEnumerateEntry> ListEntries()
    {
        var entries = new BrcEnumerateEntry[_count];
        var entriesIntPtr = EntriesIntPtr;
        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = GetEntry(ref entriesIntPtr);
        }
        return entries;
    }

    static BrcEnumerateEntry GetEntry(ref IntPtr entryLocationPtr)
    {
        var entrySignaturePtr = (long*)entryLocationPtr + SignatureLongOffset;
        var signaturePtr = (TSignature*)entrySignaturePtr;
        var aggregatePtr = (TAggregate*)(entrySignaturePtr + FromSignatureLongOffsetAggregate);
        var keyPtr = (byte*)(entrySignaturePtr + FromSignatureLongOffsetKey);
        var keySpan = new ReadOnlySpan<byte>(keyPtr, signaturePtr->KeyLength);
        var name = Encoding.UTF8.GetString(keySpan);
        var entry = new BrcEnumerateEntry(name, *aggregatePtr);

        // TODO: Might group keys differently!
        var keyLongSize = (uint)((signaturePtr->KeyLength + sizeof(long) - 1) / sizeof(long));
        entryLocationPtr += (nint)(TAggregate.LongSize + NextLongSize + TSignature.LongSize + keyLongSize);

        return entry;
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
