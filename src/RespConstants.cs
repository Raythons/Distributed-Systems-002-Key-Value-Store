// ============================================================
public static class Resp
{
    public const string Ok          = "+OK\r\n";
    public const string Pong        = "+PONG\r\n";
    public const string NilBulk     = "$-1\r\n";
    public const string NilArray    = "*-1\r\n";    // BLPOP timeout: null multi-bulk
    public const string EmptyArray  = "*0\r\n";
    public const string InvalidFmt  = "-ERR invalid format\r\n";
    public const string UnknownCmd  = "-ERR unknown command\r\n";
    public const string WrongType   = "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
    public const string Crlf        = "\r\n";

    public static string Bulk(string value) =>
        $"${value.Length}{Crlf}{value}{Crlf}";

    public static string Err(string message) =>
        $"-ERR {message}{Crlf}";

    public static string Array(System.Collections.Generic.IEnumerable<string> items)
    {
        var sb = new System.Text.StringBuilder();
        int count = 0;
        foreach (var item in items)
        {
            count++;
            sb.Append($"${item.Length}{Crlf}{item}{Crlf}");
        }
        return $"*{count}{Crlf}{sb.ToString()}";
    }
}
