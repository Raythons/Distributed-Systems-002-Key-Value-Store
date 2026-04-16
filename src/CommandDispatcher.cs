// ============================================================
// CommandDispatcher — routes tokens[] to the right handler.
// ============================================================
public static class CommandDispatcher
{
    private static readonly Dictionary<string, ICommandHandler> Handlers = new(StringComparer.OrdinalIgnoreCase)
    {
        { "PING",  new PingCommand() },
        { "ECHO",  new EchoCommand() },
        { "SET",   new SetCommand() },
        { "GET",   new GetCommand() },
        { "RPUSH", new RPushCommand() }
    };

    public static string Dispatch(string input)
    {
        RespToken[]? tokens = RespParser.Parse(input);

        if (tokens is null || tokens.Length == 0)
            return Resp.InvalidFmt;

        string cmd = tokens[0].Value;

        if (Handlers.TryGetValue(cmd, out var handler))
        {
            return handler.Execute(tokens);
        }

        return Resp.Err($"unknown command '{cmd}'");
    }
}
