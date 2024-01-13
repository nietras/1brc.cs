using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using static nietras.BrcOps;

namespace nietras;

// 1 cache line
[StructLayout(LayoutKind.Explicit, Size = 64)]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public unsafe struct BrcEntry64
{
    [FieldOffset(00)] public fixed long Data8[8];

    [FieldOffset(00)] public Vector256<byte> Key0;
    //[FieldOffset(032)] public long Hash;
    //[FieldOffset(040)] public long Sum;
    //[FieldOffset(048)] public short Next;
    //[FieldOffset(050)] public short Length;
    //[FieldOffset(052)] public int Count;
    //[FieldOffset(056)] public short Min;
    //[FieldOffset(058)] public short Max;
    [FieldOffset(032)] public BrcEntry64* Next;
    [FieldOffset(040)] public long Sum;
    [FieldOffset(048)] public int Count;
    [FieldOffset(052)] public short Min;
    [FieldOffset(054)] public short Max;
    [FieldOffset(056)] public short Length;

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
