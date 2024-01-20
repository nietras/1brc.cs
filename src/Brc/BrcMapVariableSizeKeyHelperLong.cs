using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace nietras;

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
    public static void StoreKey(in long key, short keyLength, long* destination)
    {
        Debug.Assert(keyLength <= sizeof(long));
        *destination = key;
    }
}
