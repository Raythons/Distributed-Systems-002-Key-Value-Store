// ============================================================
// BLPOP key [key ...] timeout
//
// Blocking Left POP. Two phases:
//   Phase 1 — If any key already has data, pop and return immediately.
//   Phase 2 — Otherwise, register a TaskCompletionSource on every key
//             and await it. LPush/RPush will call NotifyWaiters which
//             resolves the TCS with the key name.  A separate Task.Delay
//             resolves it with null when the timeout fires.
//
// Response: *2 array [key, value]  — or  *-1 (NilArray) on timeout.
// ============================================================
public class BlPopCommand : IAsyncCommandHandler
{
    public string Name => "BLPOP";

    public async Task<string> ExecuteAsync(RespToken[] tokens)
    {
        // Need: BLPOP key [key ...] timeout  → at least 3 tokens
        if (tokens.Length < 3)
            return Resp.Err("wrong number of arguments for 'blpop'");

        // Last token is the timeout (float seconds; 0 = block forever)
        if (!double.TryParse(
                tokens[^1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double timeoutSeconds) || timeoutSeconds < 0)
            return Resp.Err("timeout is not a float or out of range");

        // Middle tokens are the keys
        string[] keys = tokens[1..^1].Select(t => t.Value).ToArray();

        // ----------------------------------------------------------
        // Phase 1: non-blocking check — return immediately if any
        //          key already holds data.
        // ----------------------------------------------------------
        foreach (var key in keys)
        {
            var (popped, error) = Store.LPop(key, 1);
            if (error != null) return error;
            if (popped is { Count: > 0 })
                return Resp.Array(new List<string> { key, popped[0] });
        }

        // ----------------------------------------------------------
        // Phase 2: block — create ONE TCS shared across all keys.
        //          The first LPush/RPush that hits any of these keys
        //          resolves it with that key's name.
        // ----------------------------------------------------------
        var tcs = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        foreach (var key in keys)
            Store.RegisterWaiter(key, tcs);

        // Arm the timeout (0 means block forever — no timer)
        if (timeoutSeconds > 0)
        {
            _ = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))
                    .ContinueWith(_ => tcs.TrySetResult(null));
        }

        Console.WriteLine($"[BLPOP] Client blocking on keys: [{string.Join(", ", keys)}] timeout={timeoutSeconds}s");

        string? readyKey = await tcs.Task;

        // ----------------------------------------------------------
        // Resolved — either a key got data, or we timed out.
        // ----------------------------------------------------------
        if (readyKey is null)
        {
            Console.WriteLine("[BLPOP] Timed out, returning nil.");
            return Resp.NilArray;
        }

        Console.WriteLine($"[BLPOP] Woken by key '{readyKey}', popping...");

        // The element was already pushed; pop it now.
        // (Another client could have grabbed it in a race — return nil if so)
        var (result, err) = Store.LPop(readyKey, 1);
        if (err != null) return err;
        if (result is null || result.Count == 0)
        {
            Console.WriteLine("[BLPOP] Race — element already taken, returning nil.");
            return Resp.NilArray;
        }

        return Resp.Array(new List<string> { readyKey, result[0] });
    }
}
