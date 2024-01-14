using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace nietras;

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
