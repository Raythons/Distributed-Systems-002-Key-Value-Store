using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// ============================================================
// Store: the single source of truth for all key/value data.
// In our single-threaded model, we use simple Dictionaries 
// because only one command/task runs at a time on the main thread.
// ============================================================
public record struct StoreEntry(RedisValue Value, DateTime? Expiry);

public static class Store
{
    // Core data storage - no longer needs to be Concurrent
    public static Dictionary<string, StoreEntry> Storage { get; } = new(StringComparer.Ordinal);

    // ----------------------------------------------------------
    // Waiter queues for BLPOP.
    // Each queue entry is a TCS that resolves with the key name
    // that received data, waking the waiting client.
    // ----------------------------------------------------------
    private static readonly Dictionary<string, Queue<TaskCompletionSource<string?>>> _waiters
        = new(StringComparer.Ordinal);

    // Called by BlPopCommand — registers interest in a key.
    public static void RegisterWaiter(string key, TaskCompletionSource<string?> tcs)
    {
        if (!_waiters.TryGetValue(key, out var queue))
        {
            queue = new Queue<TaskCompletionSource<string?>>();
            _waiters[key] = queue;
        }
        queue.Enqueue(tcs);
    }

    // Called by LPush / RPush after writing data.
    // Wakes the FIRST waiting client for this key.
    private static void NotifyWaiters(string key)
    {
        if (!_waiters.TryGetValue(key, out var queue)) return;

        while (queue.Count > 0)
        {
            var tcs = queue.Dequeue();
            // TrySetResult returns false if the client already timed out — skip it.
            if (tcs.TrySetResult(key)) break;
        }

        if (queue.Count == 0)
            _waiters.Remove(key);
    }

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
            Storage.Remove(key);
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
    // GetList — Helper for list-specific commands.
    // Returns (list, error)
    // ----------------------------------------------------------
    public static (RedisList? list, string? error) GetList(string key)
    {
        var entry = GetEntry(key);
        if (entry is null) return (null, null);

        if (entry.Value.Value is not RedisList rl)
            return (null, Resp.WrongType);

        return (rl, null);
    }

    public static (int length, string? error) RPush(string key, string[] elements)
    {
        var entry = GetEntry(key);
        RedisList list;

        if (entry is null)
        {
            list = new RedisList();
            Storage[key] = new StoreEntry(list, null);
        }
        else if (entry.Value.Value is RedisList existingList)
        {
            list = existingList;
        }
        else
        {
            return (0, Resp.WrongType);
        }

        int len = list.RPush(elements);
        NotifyWaiters(key);   // Wake any BLPOP clients waiting on this key
        return (len, null);
    }

    public static (int length, string? error) LPush(string key, string[] elements)
    {
        var entry = GetEntry(key);
        RedisList list;

        if (entry is null)
        {
            list = new RedisList();
            Storage[key] = new StoreEntry(list, null);
        }
        else if (entry.Value.Value is RedisList existingList)
        {
            list = existingList;
        }
        else
        {
            return (0, Resp.WrongType);
        }

        int len = list.LPush(elements);
        NotifyWaiters(key);   // Wake any BLPOP clients waiting on this key
        return (len, null);
    }

    // ----------------------------------------------------------
    // StartActiveExpirerAsync — background task that runs on the event loop.
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

                // Take a sample of keys that have an expiry
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
                        Storage.Remove(kv.Key);
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

    public static (int length, string? error) LLen(string key)
    {
        var entry = GetEntry(key);
        if (entry is null) return (0, null);

        if (entry.Value.Value is not RedisList rl)
            return (0, Resp.WrongType);

        return (rl.Count, null);
    }

    public static (List<string>? items, string? error) LPop(string key, int count = 1)
    {
        var (list, error) = GetList(key);
        if (error != null) return (null, error);
        
        if (list == null || list.Count == 0)
            return (null, null);

        var popped = list.LPop(count);

        // Redis cleans up empty keys automatically
        if (list.Count == 0)
        {
            Storage.Remove(key);
        }

        return (popped, null);
    }
}