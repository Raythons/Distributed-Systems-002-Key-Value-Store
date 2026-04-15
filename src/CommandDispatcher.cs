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
            return "-ERR invalid format\r\n";

        return tokens[0].Value.ToUpper() switch
        {
            "PING" => "+PONG\r\n",
            "ECHO" => HandleEcho(tokens),
            "SET"  => HandleSet(tokens),
            "GET"  => HandleGet(tokens),
            var cmd => $"-ERR unknown command '{cmd}'\r\n"
        };
    }

    // ----------------------------------------------------------
    // ECHO handler
    // tokens[0] = "ECHO", tokens[1] = the message
    // ----------------------------------------------------------
    private static string HandleEcho(RespToken[] tokens)
    {
        if (tokens.Length < 2)
            return "-ERR wrong number of arguments for 'echo'\r\n";

        string arg = tokens[1].Value;
        int len    = tokens[1].DeclaredLength;
        return $"${len}\r\n{arg}\r\n";
    }

    // ----------------------------------------------------------
    // SET handler — owns its own flag parsing (EX, PX, NX, XX)
    // tokens[0]="SET"  tokens[1]=key  tokens[2]=value  [flags...]
    // ----------------------------------------------------------
    private static string HandleSet(RespToken[] tokens)
    {
        if (tokens.Length < 3)
            return "-ERR wrong number of arguments for 'set'\r\n";

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
        return "+OK\r\n";
    }

    // ----------------------------------------------------------
    // GET handler — lazy expiration happens inside Store.Get()
    // tokens[0]="GET"  tokens[1]=key
    // ----------------------------------------------------------
    private static string HandleGet(RespToken[] tokens)
    {
        if (tokens.Length < 2)
            return "-ERR wrong number of arguments for 'get'\r\n";

        string key    = tokens[1].Value;
        string? value = Store.Get(key);

        if (value is null)
            return "$-1\r\n"; // RESP nil bulk string — key missing or expired

        return $"${value.Length}\r\n{value}\r\n";
    }
}
