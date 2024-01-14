using System;
using System.Diagnostics;
using System.Runtime.Intrinsics;
using static nietras.BrcOps;

namespace nietras;

public unsafe sealed class BrcMap128 : BrcMap<BrcEntry128>
{
    public BrcMap128(uint minCapacity) : base(minCapacity)
    { }

    internal void AddOrAggregateNewKeyValue(
        Vector256<byte> _key0, Vector256<byte> _key1, Vector256<byte> _key2, Vector256<byte> _key3,
        short keyLength, short value)
    {
        var capacity = _capacity;
        var count = _count;
        var entriesPtr = _entries;

        var key0 = _key0;
        var key1 = _key1;
        var key2 = _key2;
        var key3 = _key3;

        var hash = Hash(key0);
        hash = hash * 31 + Hash(key1);
        hash = hash * 31 + Hash(key2);
        hash = hash * 31 + Hash(key3);
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
            var vectorEquals0 = Vector256.Equals(key0, entryPtr->Key0);
            var vectorEquals1 = Vector256.Equals(key1, entryPtr->Key1);
            var vectorEquals2 = Vector256.Equals(key2, entryPtr->Key2);
            var vectorEquals3 = Vector256.Equals(key3, entryPtr->Key3) |
                Vector256.Create(0, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF).AsByte();
            var and01 = Vector256.BitwiseAnd(vectorEquals0, vectorEquals1);
            var and23 = Vector256.BitwiseAnd(vectorEquals2, vectorEquals3);
            var and = Vector256.BitwiseAnd(and01, and23);
            var mask = Vector256.ExtractMostSignificantBits(and);
            var equals = mask == uint.MaxValue;
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
                    Trace.WriteLine($"MAP128 COLLISIONS {collisions}");
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
            Trace.WriteLine($"MAP128 COLLISIONS {collisions}");
        }
#endif

        if (count < capacity)
        {
            var newEntryPtr = entriesPtr + count;
            newEntryPtr->Key0 = key0;
            newEntryPtr->Key1 = key1;
            newEntryPtr->Key2 = key2;
            newEntryPtr->Key3 = key3;
            //entryPtr->Hash = hash;
            newEntryPtr->Next = bucketEntryPtr; // *bucketPtr;
            newEntryPtr->Length = keyLength;
            newEntryPtr->Sum = value;
            newEntryPtr->Count = 1;
            newEntryPtr->Min = value;
            newEntryPtr->Max = value;

            Debug.Assert(keyLength == newEntryPtr->Length);
            Debug.Assert(newEntryPtr->Length != 0);

            *bucketPtr = newEntryPtr; //(short)count;
            ++_count;
        }
        else
        {
            Debug.Assert(false);
        }

    }

    public void AddOrAggregateEntry(BrcEntry128* otherPtr)
    {
        var capacity = _capacity;
        var count = _count;
        var entriesPtr = _entries;

        var key0 = otherPtr->Key0;
        var key1 = otherPtr->Key1;
        var key2 = otherPtr->Key2;
        var key3Unmasked = otherPtr->Key3;
        var key3 = key3Unmasked & Vector256.Create(0xFFFFFFFF, 0, 0, 0, 0, 0, 0, 0).AsByte();

        //var hash = otherPtr->Hash;
        var hash = Hash(key0);
        hash = hash * 31 + Hash(key1);
        hash = hash * 31 + Hash(key2);
        hash = hash * 31 + Hash(key3);
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
            var vectorEquals0 = Vector256.Equals(key0, entryPtr->Key0);
            var vectorEquals1 = Vector256.Equals(key1, entryPtr->Key1);
            var vectorEquals2 = Vector256.Equals(key2, entryPtr->Key2);
            var vectorEquals3 = Vector256.Equals(key3, entryPtr->Key3) |
                Vector256.Create(0, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF).AsByte();
            var and01 = Vector256.BitwiseAnd(vectorEquals0, vectorEquals1);
            var and23 = Vector256.BitwiseAnd(vectorEquals2, vectorEquals3);
            var and = Vector256.BitwiseAnd(and01, and23);
            var mask = Vector256.ExtractMostSignificantBits(and);
            var equals = mask == uint.MaxValue;
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

    public void AddOrAggregateMap(BrcMap128 map)
    {
        for (var i = 0; i < map._count; i++)
        {
            var entryPtr = map._entries + i;
            AddOrAggregateEntry(entryPtr);
        }
    }
}
