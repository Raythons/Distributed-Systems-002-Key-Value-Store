using System.Collections.Concurrent;
// ============================================================
// Store: the single source of truth for all key/value data.
// ConcurrentDictionary because multiple client Tasks + the
// background expirer can all touch Storage at the same time.
// ============================================================
public record struct StoreEntry(string Value, DateTime? Expiry);

public static class Store
{
    public static ConcurrentDictionary<string, StoreEntry> Storage { get; } = new();

    public static void Set(string key, string value, TimeSpan? ttl = null)
    {
        DateTime? expiry = ttl.HasValue ? DateTime.UtcNow.Add(ttl.Value) : null;
        Storage[key] = new StoreEntry(value, expiry);
    }

    // ----------------------------------------------------------
    // Get — lazy expiration: check TTL on every read
    // Returns null if the key is missing or has expired.
    // ----------------------------------------------------------
    public static string? Get(string key)
    {
        if (!Storage.TryGetValue(key, out StoreEntry entry))
            return null; // key doesn't exist

        // Lazy expiration check
        if (entry.Expiry.HasValue && DateTime.UtcNow > entry.Expiry.Value)
        {
            Storage.TryRemove(key, out _);
            Console.WriteLine($"[Store] Lazy-expired key: '{key}'");
            return null;
        }

        return entry.Value;
    }


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

                // Collect up to 20 keys that actually have a TTL set
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

                // If >25% of the sample was expired
                // much more garbage — run again without sleeping.
                if (expiredCount > candidates.Count * 0.25)
                {
                    shouldRunAgain = true;
                    Console.WriteLine($"[Expirer] {expiredCount}/{candidates.Count} expired — running again immediately.");
                }

            } while (shouldRunAgain);

            // Sleep before the next sweep
            await Task.Delay(100);
        }
    }
}