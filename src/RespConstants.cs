// ============================================================
public static class Resp
{
    public const string Ok          = "+OK\r\n";
    public const string Pong        = "+PONG\r\n";
    public const string NilBulk     = "$-1\r\n";
    public const string InvalidFmt  = "-ERR invalid format\r\n";
    public const string UnknownCmd  = "-ERR unknown command\r\n";
    public const string WrongType   = "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
    public const string Crlf        = "\r\n";

    public static string Bulk(string value) =>
        $"${value.Length}{Crlf}{value}{Crlf}";

    public static string Err(string message) =>
        $"-ERR {message}{Crlf}";
}
