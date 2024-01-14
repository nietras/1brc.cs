using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace nietras;

public static class BrcOps
{
    public static string FormatStats(short min, long sum, int count, short max) =>
        $"{DivTenRound(min):F1}/{DivTenRound((double)sum / count):F1}/{DivTenRound(max):F1}";

    public static double DivTenRound(double value) =>
        Math.Round(value, MidpointRounding.AwayFromZero) * 0.1;

    //public static string FormatStats(short min, long sum, int count, short max) =>
    //    $"{DivTenRound(min):F1}/{Round(sum / (double)(10 * count)):F1}/{DivTenRound(max):F1}";
    //public static double DivTenRound(double value) => Round(value / 10.0);
    //public static double Round(double value) => Math.Round(value * 10.0) / 10.0;
    // Java ones
    //    return round(min / 10.) + "/" + round(sum / (double)(10 * count)) + "/" + round(max / 10.);
    //    return Math.round(value * 10.0) / 10.0;

    //Math.Round(value* 10.0) / 10.0;
    //Math.Round(value, MidpointRounding.AwayFromZero) * 0.1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe static (int nextStartOffset, short measurement) ParseMeasurement(byte* startMeasurement)
    {
        const long dotBits = 0x10101000;
        const long multiplier = (100 * 0x1000000 + 10 * 0x10000 + 1);

        var word = Unsafe.ReadUnaligned<long>(startMeasurement);
        var invWord = ~word;
        var decimalSepPos = BitOperations.TrailingZeroCount(invWord & dotBits);
        var signed = (invWord << 59) >> 63;
        var designMask = ~(signed & 0xFF);
        var digits = ((word & designMask) << (28 - decimalSepPos)) & 0x0F000F0F00L;
        var absValue = ((digits * multiplier) >>> 32) & 0x3FF;
        var measurement = (short)((absValue ^ signed) - signed);

        var nextStartOffsetFromMeasurementStart = (decimalSepPos >> 3) + 3;

        return (nextStartOffsetFromMeasurementStart, measurement);
    }

    public static ulong Munge(ulong hash)
    {
        // https://github.com/dotnet/runtime/blob/300013b6dffdfeb1864ad5a6a929c75f9278fe50/src/coreclr/vm/comutilnative.cpp#L1869-L1871
        // To get less colliding and more evenly distributed hash codes, munge
        // the hash with two big prime numbers.
        return hash * 711650207 + 2506965631U;
    }

    // FNV1a currently best - but not ideal
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Hash(Vector256<byte> key)
    {
        if (Avx2.IsSupported)
        {
            return FNV1a32_Single_AVX2(key);
        }
        else
        {
            return (int)Primes_Single_Vector256(key);
        }
    }

    // FNV-1a hash function for a single __m256i using AVX2
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int FNV1a32_Single_AVX2(Vector256<byte> key)
    {
        // FNV-1a constants
        var prime = Vector256.Create(0x01000193);
        var offset = Vector256.Create(unchecked((int)0x811c9dc5));

        // Initialize hash value
        var hash = offset;

        // Hash a single __m256i
        hash = Vector256.Xor(hash, key.AsInt32()); // _mm256_xor_si256(hash, data);
        hash = Avx2.MultiplyLow(hash, prime);      // _mm256_mullo_epi32(hash, prime);

        // Horizontal addition
        hash = Avx2.HorizontalAdd(hash, hash);     //_mm256_hadd_epi32(hash, hash);
        hash = Avx2.HorizontalAdd(hash, hash);     //_mm256_hadd_epi32(hash, hash);

        return hash[0];
    }

    // CityHash for a single Vector256<int> using AVX2
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong CityHash64_Single_AVX2(Vector256<byte> key)
    {
        const ulong kMul = 0x9ddfea08eb382d69UL;
        const ulong seed = 0xc70f6907UL;
        var mul = Vector256.Create(kMul).AsInt32();
        var s = Vector256.Create(seed).AsInt32();
        var mix = Vector256.Create(0x9e3779b9).AsInt32();

        // Convert Vector256<int> to Vector256<ulong>
        var vData = key.AsInt32();

        // Mix the data
        var a = Avx2.MultiplyLow(vData, mul);
        var b = Avx2.Add(a, s);
        var c = Avx2.MultiplyLow(b, mul);

        // Perform a 64-bit to 32-bit mix
        var result = Avx2.MultiplyLow(Vector256.AsInt32(c), mix).AsUInt64();
        return result[0] ^ result[2];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Primes_Single_Vector256(Vector256<byte> key)
    {
        // Below much slower and about same collisions (inspired by java folks)
        //var longs = key.AsInt64();
        //var l0 = longs[0];
        //var l1 = longs[1];
        //var l2 = longs[2];
        //var l3 = longs[3];
        //var hash = 1L;
        //hash ^= l0;
        //hash ^= l1;
        //hash ^= l2;
        //hash ^= l3;
        //return hash ^ (hash >>> 33);

        // Primes- 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97.
        // Dumb hash - ad hoc tested by tracing collision count
        var hashMultiplier = Vector256.Create(17, 19, 23, 29, 31, 37, 41, 43);
        var as32 = key.AsInt32();
        var product256 = Vector256.Multiply(as32, hashMultiplier);
        var xor128 = (Vector128.Xor(product256.GetLower(), product256.GetUpper())).AsInt64();
        var hash = Vector128.Sum(xor128);

        // TRY: HorizontalAdds instead

        // Extra entropy (improves hash and keeps collisions <= 1 otherwise more than 10!)
        //return hash ^ (hash >>> 33);
        // Yet worse hash is faster? (could this be due to better cache locality?)
        // Is it then better to keep hash map small at start (and grow) instead of pre-alloc 10k?
        return hash;

        //var xor128 = (Vector128.Xor(key.GetLower(), key.GetUpper())).AsInt64();
        //return Vector128.Sum(xor128);

        //var lower = xor128[0];
        //var upper = xor128[1];
        //return lower * 31 + upper;
    }
}
