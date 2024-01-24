using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace nietras;

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
        var keyEquals = Vector256.Equals(key, entryKey);
        var keyBitMaskEquals = keyEquals.ExtractMostSignificantBits();
        var equalsBitMask = signatureBitEquals | keyBitMaskEquals;
        return equalsBitMask == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void StoreKey(in Vector256<byte> key, short keyLength, long* destination)
    {
        Debug.Assert(keyLength <= sizeof(Vector256<byte>));
        Vector256.Store(key, (byte*)destination);
    }
}
