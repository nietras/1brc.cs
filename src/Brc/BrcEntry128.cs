using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using static nietras.BrcOps;

namespace nietras;

// 2 cache lines
[StructLayout(LayoutKind.Explicit, Size = 128)]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public unsafe struct BrcEntry128
{
    // Station names are <= 100 bytes long use below for that with masking for
    // last 4 bytes.

    [FieldOffset(00)] public fixed long Data8[16];

    [FieldOffset(00)] public Vector256<byte> Key0;
    [FieldOffset(32)] public Vector256<byte> Key1;
    [FieldOffset(64)] public Vector256<byte> Key2;
    [FieldOffset(96)] public Vector256<byte> Key3; // Or use 128 bit vectors, long or int?
    [FieldOffset(100)] public short Min;
    [FieldOffset(102)] public short Max;
    [FieldOffset(104)] public BrcEntry128* Next;
    [FieldOffset(112)] public long Sum;
    [FieldOffset(120)] public int Count;
    [FieldOffset(124)] public short Length;
    //[FieldOffset(?)] public long Hash;
    //[FieldOffset(?)] public short Next;

    public string Name => GetName();

    internal string DebuggerDisplay => $"{Name} - {DivTenRound(Min):F1}/{DivTenRound((double)Sum / Count):F1}/{DivTenRound(Max):F1} - {Count}";

    unsafe string GetName()
    {
        fixed (long* ptr = Data8)
        {
            var bytes = new Span<byte>(ptr, Length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
