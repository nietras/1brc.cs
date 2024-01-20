using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace nietras;

readonly record struct Vector256x2(Vector256<byte> Vector0, Vector256<byte> Vector1);

unsafe abstract class BrcMapVariableSizeKeyHelperVector256x2
    : IBrcMapVariableSizeKeyHelper<Vector256x2, long, long>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Hash(in Vector256x2 key) =>
        // TODO: Make better hash of both
        (uint)(BrcOps.Hash(key.Vector0) + 31 * BrcOps.Hash(key.Vector1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long CreateSignature(short keyLength, uint hash) => keyLength;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long MaybeCombineSignatureKey(in long signature, in Vector256x2 key) => signature;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreSignatureKeyEqual(in long signature, in Vector256x2 key, long* entrySignaturePtr)
    {
        var entrySignature = *entrySignaturePtr;
        var signatureBitEquals = signature ^ entrySignature;

        var entryKey0 = Vector256.Load((byte*)(entrySignaturePtr + 1));
        var keyBitEquals0 = key.Vector0 ^ entryKey0;
        var keyBitMaskEquals0 = keyBitEquals0.ExtractMostSignificantBits();

        var entryKey1 = Vector256.Load((byte*)(entrySignaturePtr + 1 + Vector256<byte>.Count));
        var keyBitEquals1 = key.Vector1 ^ entryKey1;
        var keyBitMaskEquals1 = keyBitEquals1.ExtractMostSignificantBits();

        var equalsBitMask = signatureBitEquals | keyBitMaskEquals0 | keyBitMaskEquals1;
        return equalsBitMask == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void StoreKey(in Vector256x2 key, short keyLength, long* destination)
    {
        Debug.Assert(keyLength <= sizeof(Vector256x2));
        Vector256.Store(key.Vector0, (byte*)destination);
        Vector256.Store(key.Vector1, (byte*)destination + Vector256<byte>.Count);
    }
}
