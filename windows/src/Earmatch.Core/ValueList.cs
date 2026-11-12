using System.Collections;
using System.Runtime.CompilerServices;

namespace Earmatch.Core;

/// A read-only list that compares by its elements, like a Swift array inside a struct, so records holding lists keep
/// value equality.
[CollectionBuilder(typeof(ValueList), nameof(ValueList.Create))]
public sealed class ValueList<T> : IReadOnlyList<T>, IEquatable<ValueList<T>>
{
    private readonly T[] _items;

    public ValueList(IEnumerable<T> items) => _items = items.ToArray();

    public static ValueList<T> Empty { get; } = new([]);

    public int Count => _items.Length;
    public T this[int index] => _items[index];

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    public bool Equals(ValueList<T>? other) => other is not null && _items.SequenceEqual(other._items);
    public override bool Equals(object? obj) => Equals(obj as ValueList<T>);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _items) hash.Add(item);
        return hash.ToHashCode();
    }

    public static implicit operator ValueList<T>(T[] items) => new(items);
    public static implicit operator ValueList<T>(List<T> items) => new(items);
}

public static class ValueList
{
    public static ValueList<T> Create<T>(ReadOnlySpan<T> items) => new(items.ToArray());

    public static ValueList<T> ToValueList<T>(this IEnumerable<T> items) => new(items);
}
