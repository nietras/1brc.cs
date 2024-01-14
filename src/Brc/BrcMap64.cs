using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using static nietras.BrcOps;

namespace nietras;

public unsafe sealed class BrcMap64 : BrcMap<BrcEntry64>
{
    public BrcMap64(uint minCapacity) : base(minCapacity)
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddOrAggregateNewKeyValue(Vector256<byte> key, short keyLength, short value)
    {
        var capacity = _capacity;
        var count = _count;
        var entriesPtr = _entries;

        var hash = Hash(key);
#if PRIMES
        var bucketIndex = _primeInfo.GetIndexForHash((uint)hash);
#else
        var bucketIndex = hash & (capacity - 1);
#endif

        var bucketPtr = _buckets + bucketIndex;
        //var entriesIndex = *bucketPtr;
        var bucketEntryPtr = *bucketPtr;
#if DEBUG
        var collisions = 0;
#endif
        //while ((uint)entriesIndex < count)
        for (var entryPtr = bucketEntryPtr; entryPtr != null; entryPtr = entryPtr->Next)
        {
            //var entryPtr = entriesPtr + entriesIndex;
            // Compare key only - this assumes no key is only zero bytes! Otherwise, must include length.
            var entryKey = Vector256.LoadAligned((byte*)entryPtr);
            var equals = entryKey.Equals(key);
            // Compare everything incl. hash even though not needed
            //var equals = entryPtr->Hash == hash && entryPtr->Length == keyLength && key.Equals(entryPtr->Vector0);
            if (equals)
            {
                entryPtr->Count++;
                entryPtr->Sum += value;
                entryPtr->Min = Math.Min(entryPtr->Min, value);
                entryPtr->Max = Math.Max(entryPtr->Max, value);
#if DEBUG
                if (collisions > 1)
                {
                    Trace.WriteLine($"MAP064 COLLISIONS {collisions}");
                }
#endif
                return;
            }
            //entriesIndex = entryPtr->Next;
#if DEBUG
            ++collisions;
#endif
        }
#if DEBUG
        if (collisions > 1)
        {
            Trace.WriteLine($"MAP064 COLLISIONS {collisions}");
        }
#endif

        if (count < capacity)
        {
            var newEntryPtr = entriesPtr + count;
            Vector256.StoreAligned(key, (byte*)newEntryPtr);
            //entryPtr->Hash = hash;
            newEntryPtr->Next = bucketEntryPtr; // *bucketPtr;
            newEntryPtr->Length = keyLength;
            newEntryPtr->Sum = value;
            newEntryPtr->Count = 1;
            newEntryPtr->Min = value;
            newEntryPtr->Max = value;

            *bucketPtr = newEntryPtr; //(short)count;
            ++_count;
        }
        else
        {
            Debug.Assert(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddOrAggregateEntry(BrcEntry64* otherPtr)
    {
        var capacity = _capacity;
        var count = _count;
        var entriesPtr = _entries;

        //var hash = otherPtr->Hash;
        var hash = Hash(otherPtr->Key0);
#if PRIMES
        var bucketIndex = _primeInfo.GetIndexForHash((uint)hash);
#else
        var bucketIndex = hash & (capacity - 1);
#endif

        var bucketPtr = _buckets + bucketIndex;
        //var entriesIndex = *bucketPtr;
        var bucketEntryPtr = *bucketPtr;

        //var entriesIndex = *bucketPtr;
        //while ((uint)entriesIndex < count)
        for (var entryPtr = bucketEntryPtr; entryPtr != null; entryPtr = entryPtr->Next)
        {
            //var entryPtr = entriesPtr + entriesIndex;
            // Compare key only - this assumes no key is only zero bytes! Otherwise, must include length.
            var entryKey = Vector256.LoadAligned((byte*)entryPtr);
            var equals = entryKey.Equals(Vector256.LoadAligned((byte*)otherPtr));
            // Compare everything incl. hash even though not needed
            //var equals = entryPtr->Hash == hash && entryPtr->Length == keyLength && key.Equals(entryPtr->Vector0);
            if (equals)
            {
                entryPtr->Sum += otherPtr->Sum;
                entryPtr->Count += otherPtr->Count;
                entryPtr->Min = Math.Min(entryPtr->Min, otherPtr->Min);
                entryPtr->Max = Math.Max(entryPtr->Max, otherPtr->Max);
                return;
            }
            //entriesIndex = entryPtr->Next;
        }

        if (count < capacity)
        {
            var newEntryPtr = entriesPtr + count;
            // Copy
            *newEntryPtr = *otherPtr;
            newEntryPtr->Next = bucketEntryPtr;
            //*bucketPtr = (short)count;
            *bucketPtr = newEntryPtr;
            ++_count;
        }
        else
        {
            Debug.Assert(false);
        }
    }

    public void AddOrAggregateMap(BrcMap64 map)
    {
        for (var i = 0; i < map._count; i++)
        {
            var entryPtr = map._entries + i;
            AddOrAggregateEntry(entryPtr);
        }
    }
}
