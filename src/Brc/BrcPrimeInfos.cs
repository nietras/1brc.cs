using System.Diagnostics;

namespace nietras;

public static class BrcPrimeInfos
{
    public static BrcPrimeInfo NextPrime(uint number)
    {
        for (var i = 0; i < s_primeInfo.Length; i++)
        {
            ref var primeInfo = ref s_primeInfo[i];
            if (primeInfo.Prime >= number)
            {
                return primeInfo;
            }
        }
        Debug.Assert(false, $"{number} is not supported by prime info table");
        return s_primeInfo[^1];
    }

    // Table of primes and their magic-number-divide constant.
    // For more info see the book "Hacker's Delight" chapter 10.9 "Unsigned Division by Divisors >= 1"
    // These were selected by looking for primes, each roughly twice as big as the next, having
    // 32-bit magic numbers, (because the algorithm for using 33-bit magic numbers is slightly slower).
    // https://github.com/dotnet/runtime/blob/300013b6dffdfeb1864ad5a6a929c75f9278fe50/src/coreclr/gcinfo/simplerhash.cpp#L12-L40
    public static readonly BrcPrimeInfo[] s_primeInfo =
    {
        new(9,         0x38e38e39, 1),
        new(23,        0xb21642c9, 4),
        new(59,        0x22b63cbf, 3),
        new(131,       0xfa232cf3, 7),
        new(239,       0x891ac73b, 7),
        new(433,       0x975a751,  4),
        new(761,       0x561e46a5, 8),
        new(1399,      0xbb612aa3, 10),
        new(2473,      0x6a009f01, 10),
        new(4327,      0xf2555049, 12),
        new(7499,      0x45ea155f, 11),
        new(12973,     0x1434f6d3, 10),
        new(22433,     0x2ebe18db, 12),
        new(46559,     0xb42bebd5, 15),
        new(96581,     0xadb61b1b, 16),
        new(200341,    0x29df2461, 15),
        new(415517,    0xa181c46d, 18),
        new(861719,    0x4de0bde5, 18),
        new(1787021,   0x9636c46f, 20),
        new(3705617,   0x4870adc1, 20),
        new(7684087,   0x8bbc5b83, 22),
        new(15933877,  0x86c65361, 23),
        new(33040633,  0x40fec79b, 23),
        new(68513161,  0x7d605cd1, 25),
        new(142069021, 0xf1da390b, 27),
        new(294594427, 0x74a2507d, 27),
        new(733045421, 0x5dbec447, 28),
    };
}
