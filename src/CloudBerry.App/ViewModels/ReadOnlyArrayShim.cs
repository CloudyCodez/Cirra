using System.Collections;
using System.Collections.Generic;

public sealed class _003C_003Ez__ReadOnlyArray<T> : IReadOnlyList<T>
{
    private readonly T[] _items;

    public _003C_003Ez__ReadOnlyArray(T[] items)
    {
        _items = items ?? [];
    }

    public int Count => _items.Length;

    public T this[int index] => _items[index];

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}

public sealed class _003C_003Ez__ReadOnlySingleElementList<T> : IReadOnlyList<T>
{
    private readonly T _item;

    public _003C_003Ez__ReadOnlySingleElementList(T item)
    {
        _item = item;
    }

    public int Count => 1;

    public T this[int index] => index == 0 ? _item : throw new ArgumentOutOfRangeException(nameof(index));

    public IEnumerator<T> GetEnumerator()
    {
        yield return _item;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
