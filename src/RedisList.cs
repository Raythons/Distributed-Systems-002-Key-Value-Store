// ============================================================
// Holds a list of strings.
// Used by: RPUSH, LPUSH, LRANGE, LLEN, etc.
// ============================================================
public class RedisList : RedisValue
{
    public override string TypeName => "list";

    private readonly List<string> _items = new();

    // Append to the right — returns new length
    public int RPush(string[] values)
    {
        _items.AddRange(values);
        return _items.Count;
    }

    // Prepend to the left — returns new length
    public int LPush(string[] values)
    {
        // InsertRange shifts the entire list once! O(N) instead of O(N * values.Length)
        // We reverse it because LPUSH a b c results in [c, b, a]
        _items.InsertRange(0, values.Reverse());
        return _items.Count;
    }

    public int Count => _items.Count;

    // LRANGE
    public List<string> GetRange(int start, int stop)
    {
        int count = _items.Count;

        if (start < 0) start = Math.Max(0, count + start);
        if (stop  < 0) stop  = count + stop;
        if (stop  >= count) stop = count - 1;

        if (start > stop) return new List<string>();

        return _items.GetRange(start, stop - start + 1);
    }

    public IReadOnlyList<string> Items => _items.AsReadOnly();
}
