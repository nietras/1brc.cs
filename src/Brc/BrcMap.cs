using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using static nietras.BrcOps;

namespace nietras;

// |-------------|------|-----|-------|
// |   TValue    | Next | L+H |  Key  |
// |-------------|------|-----|-------|
//     V * 8 b     8 b   8 b   K * 8 b
// where:
//   TValue = value stored before e.g. BRC stats = 16 bytes
//   Next = pointer to next entry at L+H
//   L+H = Length as 1 byte, Partial hash as 7 bytes (or 4 bytes)
//   Key = stored as multiples of 8 bytes e.g. station name


[StructLayout(LayoutKind.Explicit, Size = 64)]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public struct BrcAggregate
{
    [FieldOffset(00)] public long Sum;
    [FieldOffset(08)] public int Count;
    [FieldOffset(12)] public short Min;
    [FieldOffset(14)] public short Max;

    internal string DebuggerDisplay => $"{DivTenRound(Min):F1}/{DivTenRound((double)Sum / Count):F1}/{DivTenRound(Max):F1} - {Count}";
}


public unsafe class BrcMap8<TValue> : IDisposable
    where TValue : unmanaged
{
#if PRIMES
    internal BrcPrimeInfo _primeInfo;
#endif
    internal uint _capacity;
    internal long** _buckets;
    internal long* _entries;
    internal long* _entriesCurrentEndPtr;
    internal int _count = 0;
    volatile bool _disposed = false;

    public BrcMap8(uint maxBytesPerKey, uint minCapacity)
    {
#if PRIMES
        _primeInfo = BrcPrimeInfos.NextPrime(minCapacity);
        _capacity = _primeInfo.Prime;
#else
        _capacity = BitOperations.RoundUpToPowerOf2(minCapacity);
#endif
        var BytesNext = Unsafe.SizeOf<IntPtr>();
        var BytesLengthAndHash = Unsafe.SizeOf<IntPtr>();
        var maxBytesPerEntry = Unsafe.SizeOf<TValue>() + BytesNext + BytesLengthAndHash + maxBytesPerKey;

        var entriesByteCount = (nuint)(maxBytesPerEntry * _capacity);
        _entries = (long*)NativeMemory.AlignedAlloc(entriesByteCount, Brc.CacheLineSize);
#if DEBUG
        NativeMemory.Clear(_entries, entriesByteCount); // Not absolutely necessary so only DEBUG
#endif

        //var bucketsByteCount = (nuint)(Unsafe.SizeOf<short>() * _capacity);
        //_buckets = (short*)NativeMemory.AlignedAlloc(bucketsByteCount, (nuint)Unsafe.SizeOf<short>());
        //// Must fill with less than 0 is marker for no entry
        //new Span<short>(_buckets, (int)_capacity).Fill(-1);

        var bucketsByteCount = (nuint)(Unsafe.SizeOf<IntPtr>() * _capacity);
        _buckets = (long**)NativeMemory.AlignedAlloc(bucketsByteCount, (nuint)Unsafe.SizeOf<IntPtr>());
        NativeMemory.Clear(_buckets, bucketsByteCount);
    }

    //public ReadOnlySpan<short> Buckets => new(_buckets, (int)_capacity);
    public ReadOnlySpan<IntPtr> Buckets => new(_buckets, (int)_capacity);

    public ReadOnlySpan<long> Entries => new(_entries, _count);

    public int Count => _count;

    public uint Capacity => _capacity;

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


public unsafe class BrcMap<TEntry> : IDisposable
    where TEntry : unmanaged
{
#if PRIMES
    internal BrcPrimeInfo _primeInfo;
#endif
    internal uint _capacity;
    // if no entry < 0 otherwise >= 0
    //internal short* _buckets;
    internal TEntry** _buckets;
    internal TEntry* _entries;
    internal int _count = 0;
    volatile bool _disposed = false;

    public BrcMap(uint minCapacity)
    {
#if PRIMES
        _primeInfo = BrcPrimeInfos.NextPrime(minCapacity);
        _capacity = _primeInfo.Prime;
#else
        _capacity = BitOperations.RoundUpToPowerOf2(minCapacity);
#endif
        var entriesByteCount = (nuint)(Unsafe.SizeOf<TEntry>() * _capacity);
        _entries = (TEntry*)NativeMemory.AlignedAlloc(entriesByteCount, Brc.CacheLineSize);
#if DEBUG
        NativeMemory.Clear(_entries, entriesByteCount); // Not absolutely necessary so only DEBUG
#endif

        //var bucketsByteCount = (nuint)(Unsafe.SizeOf<short>() * _capacity);
        //_buckets = (short*)NativeMemory.AlignedAlloc(bucketsByteCount, (nuint)Unsafe.SizeOf<short>());
        //// Must fill with less than 0 is marker for no entry
        //new Span<short>(_buckets, (int)_capacity).Fill(-1);

        var bucketsByteCount = (nuint)(Unsafe.SizeOf<IntPtr>() * _capacity);
        _buckets = (TEntry**)NativeMemory.AlignedAlloc(bucketsByteCount, (nuint)Unsafe.SizeOf<IntPtr>());
        NativeMemory.Clear(_buckets, bucketsByteCount);
    }

    //public ReadOnlySpan<short> Buckets => new(_buckets, (int)_capacity);
    public ReadOnlySpan<IntPtr> Buckets => new(_buckets, (int)_capacity);

    public ReadOnlySpan<TEntry> Entries => new(_entries, _count);

    public int Count => _count;

    public uint Capacity => _capacity;

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
