// ============================================================
// Holds a list of strings.
// Used by: RPUSH, LPUSH, LRANGE, LLEN, etc.
// ============================================================
public class RedisList : RedisValue
{
    public override string TypeName => "list";

    private readonly List<string> _items = new();

    // Append to the right — returns new length
    public int RPush(string value)
    {
        _items.Add(value);
        return _items.Count;
    }

    // Prepend to the left — returns new length
    public int LPush(string value)
    {
        _items.Insert(0, value);
        return _items.Count;
    }

    public int Count => _items.Count;

    // LRANGE start stop — supports negative indices like real Redis
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
