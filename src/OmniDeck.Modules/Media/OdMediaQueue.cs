namespace OmniDeck.Modules.Media;

/// <summary>The files the player works through, in order, and which one is current.</summary>
internal sealed class OdMediaQueue
{
    private readonly List<string> _items = [];

    public OdMediaQueue(IEnumerable<string> items, int current)
    {
        _items.AddRange(items);
        Current = _items.Count == 0 ? -1 : Math.Clamp(current, 0, _items.Count - 1);
    }

    public IReadOnlyList<string> Items => _items;

    /// <summary>The index of the current file; -1 while the queue is empty.</summary>
    public int Current { get; private set; }

    public string? CurrentItem => Current >= 0 ? _items[Current] : null;

    public bool HasNext => Current + 1 < _items.Count;

    public bool HasPrevious => Current > 0;

    /// <summary>Adds files at the end; the first file added to an empty queue becomes current. Returns the index of the first one.</summary>
    public int Add(IEnumerable<string> paths)
    {
        var first = _items.Count;
        _items.AddRange(paths);
        if (Current < 0 && _items.Count > 0)
        {
            Current = 0;
        }

        return first;
    }

    public bool MoveTo(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            return false;
        }

        Current = index;
        return true;
    }

    public bool Next() => MoveTo(Current + 1);

    public bool Previous() => MoveTo(Current - 1);

    /// <summary>Removes one file; the current one stays current unless it was the one removed.</summary>
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            return;
        }

        _items.RemoveAt(index);
        if (index < Current || Current >= _items.Count)
        {
            Current--;
        }

        if (_items.Count == 0)
        {
            Current = -1;
        }
    }

    public void Clear()
    {
        _items.Clear();
        Current = -1;
    }
}
