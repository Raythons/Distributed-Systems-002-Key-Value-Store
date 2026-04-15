// ============================================================
// CommandDispatcher — routes tokens[] to the right handler.
// Each handler is smart about its own args/flags.
// ============================================================
public static class CommandDispatcher
{
    public static string Dispatch(string input)
    {
        RespToken[]? tokens = RespParser.Parse(input);

        if (tokens is null || tokens.Length == 0)
            return Resp.InvalidFmt;

        return tokens[0].Value.ToUpper() switch
        {
            "PING" => Resp.Pong,
            "ECHO" => HandleEcho(tokens),
            "SET"  => HandleSet(tokens),
            "GET"  => HandleGet(tokens),
            var cmd => Resp.Err($"unknown command '{cmd}'")
        };
    }

    // ----------------------------------------------------------
    private static string HandleEcho(RespToken[] tokens)
    {
        if (tokens.Length < 2)
            return Resp.Err("wrong number of arguments for 'echo'");

        return Resp.Bulk(tokens[1].Value);
    }

    // ----------------------------------------------------------
    // SET handler — owns its own flag parsing (EX, PX)
    // tokens[0]="SET"  tokens[1]=key  tokens[2]=value  [flags...]
    // ----------------------------------------------------------
    private static string HandleSet(RespToken[] tokens)
    {
        if (tokens.Length < 3)
            return Resp.Err("wrong number of arguments for 'set'");

        string key   = tokens[1].Value;
        string value = tokens[2].Value;

        TimeSpan? ttl = null;

        for (int i = 3; i < tokens.Length; i++)
        {
            switch (tokens[i].Value.ToUpper())
            {
                case "EX" when i + 1 < tokens.Length:
                    ttl = TimeSpan.FromSeconds(double.Parse(tokens[++i].Value));
                    break;
                case "PX" when i + 1 < tokens.Length:
                    ttl = TimeSpan.FromMilliseconds(double.Parse(tokens[++i].Value));
                    break;
            }
        }

        Store.Set(key, value, ttl);
        Console.WriteLine($"[SET] key='{key}' value='{value}' ttl={ttl}");
        return Resp.Ok;
    }

    // ----------------------------------------------------------
    // GET handler — lazy expiration happens inside Store.Get()
    // tokens[0]="GET"  tokens[1]=key
    // ----------------------------------------------------------
    private static string HandleGet(RespToken[] tokens)
    {
        if (tokens.Length < 2)
            return Resp.Err("wrong number of arguments for 'get'");

        string? value = Store.Get(tokens[1].Value);

        return value is null ? Resp.NilBulk : Resp.Bulk(value);
    }
}
