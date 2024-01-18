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
    const short _valueLong0 = 10;
    const short _valueLong1 = 20;

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
            new(_keyName0, new() { Sum = _valueLong0, Count = 1, Min = _valueLong0, Max = _valueLong0 }),
        };

        addOrAggregate(To<TKey>(_keySpan0), _keyLength0, _valueLong0);

        var actual = _sut.ListEntries();
        AssertEntries(expected, actual);
    }

    void AddAggregateAssertEntries<TKey>(Action<TKey, short, short> addOrAggregate)
        where TKey : unmanaged
    {
        var expected = new BrcEnumerateEntry[]
        {
            new(_keyName0, new() { Sum = (_valueLong0 + _valueLong1), Count = 2, Min = _valueLong0, Max = _valueLong1 }),
        };

        addOrAggregate(To<TKey>(_keySpan0), _keyLength0, _valueLong0);
        addOrAggregate(To<TKey>(_keySpan0), _keyLength0, _valueLong1);

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
