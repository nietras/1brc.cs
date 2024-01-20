using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace nietras.Test;

[TestClass]
public class BrcMapVariableSizeKeyTest
{
    static ReadOnlySpan<byte> _keySpan0 => "abcdefg"u8;
    static readonly string _keyName0 = Encoding.UTF8.GetString(_keySpan0);
    static readonly short _keyLength0 = (short)_keySpan0.Length;
    const short _valueLong00 = 10;
    const short _valueLong01 = 20;

    static ReadOnlySpan<byte> _keySpan1 => "hijklmno"u8;
    static readonly string _keyName1 = Encoding.UTF8.GetString(_keySpan1);
    static readonly short _keyLength1 = (short)_keySpan1.Length;
    const short _valueLong10 = 30;
    const short _valueLong11 = 40;

    readonly BrcMapVariableSizeKey _sut = new(100);

    [TestMethod]
    public void BrcMapVariableSizeKeyTest_Add_Long()
    {
        AddAssertEntries<long>(_sut.AddOrAggregateNewKeyValueLong);
    }

    [TestMethod]
    public void BrcMapVariableSizeKeyTest_Add_Aggregate_Long()
    {
        AddAggregateAssertEntries<long>(_sut.AddOrAggregateNewKeyValueLong);
    }

    [TestMethod]
    public void BrcMapVariableSizeKeyTest_Add_Vector256()
    {
        AddAssertEntries<Vector256<byte>>(_sut.AddOrAggregateNewKeyValueVector256);
    }

    [TestMethod]
    public void BrcMapVariableSizeKeyTest_Add_Aggregate_Vector256()
    {
        AddAggregateAssertEntries<Vector256<byte>>(_sut.AddOrAggregateNewKeyValueVector256);
    }

    void AddAssertEntries<TKey>(Action<TKey, short, short> addOrAggregate)
        where TKey : unmanaged
    {
        var expected = new BrcEnumerateEntry[]
        {
            new(_keyName0, new() { Sum = _valueLong00, Count = 1, Min = _valueLong00, Max = _valueLong00 }),
            new(_keyName1, new() { Sum = _valueLong10, Count = 1, Min = _valueLong10, Max = _valueLong10 }),
        };

        addOrAggregate(To<TKey>(_keySpan0), _keyLength0, _valueLong00);

        addOrAggregate(To<TKey>(_keySpan1), _keyLength1, _valueLong10);

        var actual = _sut.ListEntries();
        AssertEntries(expected, actual);
    }

    void AddAggregateAssertEntries<TKey>(Action<TKey, short, short> addOrAggregate)
        where TKey : unmanaged
    {
        var expected = new BrcEnumerateEntry[]
        {
            new(_keyName0, new() { Sum = (_valueLong00 + _valueLong01), Count = 2, Min = _valueLong00, Max = _valueLong01 }),
            new(_keyName1, new() { Sum = (_valueLong10 + _valueLong11), Count = 2, Min = _valueLong10, Max = _valueLong11 }),
        };

        addOrAggregate(To<TKey>(_keySpan0), _keyLength0, _valueLong00);
        addOrAggregate(To<TKey>(_keySpan0), _keyLength0, _valueLong01);

        addOrAggregate(To<TKey>(_keySpan1), _keyLength1, _valueLong11);
        addOrAggregate(To<TKey>(_keySpan1), _keyLength1, _valueLong10);

        var actual = _sut.ListEntries();
        AssertEntries(expected, actual);
    }

    static void AssertEntries(IReadOnlyList<BrcEnumerateEntry> expected, IReadOnlyList<BrcEnumerateEntry> actual)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            var e = expected[i];
            var a = actual[i];
            Assert.AreEqual(e, a, i.ToString());
        }
    }

    static unsafe T To<T>(ReadOnlySpan<byte> key) where T : unmanaged
    {
        Assert.IsTrue(key.Length <= sizeof(T));
        T value = default;
        var valuePtr = (byte*)&value;
        var valueSpan = new Span<byte>(valuePtr, key.Length);
        key.CopyTo(valueSpan);
        return value;
    }
}
