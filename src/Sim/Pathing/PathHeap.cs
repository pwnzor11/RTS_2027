namespace Rts.Sim.Pathing;

/// <summary>
/// Hand-written binary min-heap keyed by the total order (F, H, Cell). Because the cell index breaks every
/// tie, the pop order never depends on insertion history beyond the keys themselves.
/// </summary>
internal sealed class PathHeap
{
    private struct Entry
    {
        public long F;
        public long H;
        public int Cell;
    }

    private Entry[] _items = new Entry[256];
    private int _count;

    public int Count => _count;

    public void Clear() => _count = 0;

    private static bool Less(in Entry a, in Entry b)
    {
        if (a.F != b.F) return a.F < b.F;
        if (a.H != b.H) return a.H < b.H;
        return a.Cell < b.Cell;
    }

    public void Push(long f, long h, int cell)
    {
        if (_count == _items.Length) Array.Resize(ref _items, _count * 2);
        var e = new Entry { F = f, H = h, Cell = cell };
        int i = _count++;
        while (i > 0)
        {
            int p = (i - 1) >> 1;
            if (!Less(e, _items[p])) break;
            _items[i] = _items[p];
            i = p;
        }
        _items[i] = e;
    }

    public int Pop()
    {
        Entry top = _items[0];
        Entry last = _items[--_count];
        if (_count > 0)
        {
            int i = 0;
            while (true)
            {
                int c = 2 * i + 1;
                if (c >= _count) break;
                if (c + 1 < _count && Less(_items[c + 1], _items[c])) c++;
                if (!Less(_items[c], last)) break;
                _items[i] = _items[c];
                i = c;
            }
            _items[i] = last;
        }
        return top.Cell;
    }
}
