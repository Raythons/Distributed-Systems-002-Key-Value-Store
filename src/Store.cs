using System.Collections.Concurrent;

// ============================================================
// Store: the single source of truth for all key/value data.
// ConcurrentDictionary because multiple client Tasks + the
// background expirer can all touch Storage at the same time.
// ============================================================
public record struct StoreEntry(RedisValue Value, DateTime? Expiry);
public static class Store
{
    public static ConcurrentDictionary<string, StoreEntry> Storage { get; } = new();

    // ----------------------------------------------------------
    // Try to get the raw entry, checking for expiry.
    // Returns null if missing or expired.
    // ----------------------------------------------------------
    private static StoreEntry? GetEntry(string key)
    {
        if (!Storage.TryGetValue(key, out StoreEntry entry))
            return null;

        if (entry.Expiry.HasValue && DateTime.UtcNow > entry.Expiry.Value)
        {
            Storage.TryRemove(key, out _);
            Console.WriteLine($"[Store] Lazy-expired key: '{key}'");
            return null;
        }

        return entry;
    }

    // ----------------------------------------------------------
    // SET key value [ttl]
    // Always overwrites. Creates a RedisString.
    // ----------------------------------------------------------
    public static void Set(string key, string value, TimeSpan? ttl = null)
    {
        DateTime? expiry = ttl.HasValue ? DateTime.UtcNow.Add(ttl.Value) : null;
        Storage[key] = new StoreEntry(new RedisString(value), expiry);
    }

    // ----------------------------------------------------------
    // GET key
    // Returns null if missing, expired, or wrong type.
    // Returns Resp.WrongType error string if it's not a string.
    // ----------------------------------------------------------
    public static (string? value, string? error) Get(string key)
    {
        var entry = GetEntry(key);
        if (entry is null) return (null, null);

        if (entry.Value.Value is not RedisString rs)
            return (null, Resp.WrongType);

        return (rs.Value, null);
    }

    // ----------------------------------------------------------
    // RPUSH key element [element ...]
    // Creates list if it doesn't exist.
    // Returns (newLength, error).
    // ----------------------------------------------------------
    public static (int length, string? error) RPush(string key, string[] elements)
    {
        var entry = GetEntry(key);

        RedisList list;

        if (entry is null)
        {
            // Key doesn't exist — create a new list
            list = new RedisList();
            Storage[key] = new StoreEntry(list, null);
        }
        else if (entry.Value.Value is RedisList existingList)
        {
            list = existingList;
        }
        else
        {
            // Key exists but holds a different type
            return (0, Resp.WrongType);
        }

        int newLen = 0;
        foreach (var element in elements)
            newLen = list.RPush(element);

        return (newLen, null);
    }

    // ----------------------------------------------------------
    // StartActiveExpirerAsync — background task that runs forever.
    // Every 100ms: randomly sample up to 20 keys with TTLs,
    // delete expired ones. If >25% expired, run again immediately.
    // ----------------------------------------------------------
    public static async Task StartActiveExpirerAsync()
    {
        var rng = new Random();
        Console.WriteLine("[Expirer] Active expiration background task started.");

        while (true)
        {
            bool shouldRunAgain;

            do
            {
                shouldRunAgain = false;

                var candidates = Storage
                    .Where(kv => kv.Value.Expiry.HasValue)
                    .OrderBy(_ => rng.Next())
                    .Take(20)
                    .ToList();

                if (candidates.Count == 0) break;

                int expiredCount = 0;
                var now = DateTime.UtcNow;

                foreach (var kv in candidates)
                {
                    if (kv.Value.Expiry!.Value < now)
                    {
                        Storage.TryRemove(kv.Key, out _);
                        expiredCount++;
                        Console.WriteLine($"[Expirer] Actively expired key: '{kv.Key}'");
                    }
                }

                if (expiredCount > candidates.Count * 0.25)
                {
                    shouldRunAgain = true;
                    Console.WriteLine($"[Expirer] {expiredCount}/{candidates.Count} expired — running again immediately.");
                }

            } while (shouldRunAgain);

            await Task.Delay(100);
        }
    }
}