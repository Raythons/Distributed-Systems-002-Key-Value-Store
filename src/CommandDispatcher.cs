// ============================================================
// CommandDispatcher — routes tokens[] to the right handler.
//
// Two handler tables:
//   _sync  — regular ICommandHandler (returns string directly)
//   _async — IAsyncCommandHandler (returns Task<string>, used for
//            blocking commands like BLPOP)
//
// DispatchAsync handles both; callers always await it.
// ============================================================
public static class CommandDispatcher
{
    private static readonly Dictionary<string, ICommandHandler> _sync =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "PING",   new PingCommand()   },
            { "ECHO",   new EchoCommand()   },
            { "SET",    new SetCommand()    },
            { "GET",    new GetCommand()    },
            { "RPUSH",  new RPushCommand()  },
            { "LPUSH",  new LPushCommand()  },
            { "LPOP",   new LPopCommand()   },
            { "LLEN",   new LlenCommand()   },
            { "LRANGE", new LRangeCommand() },
        };

    private static readonly Dictionary<string, IAsyncCommandHandler> _async =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "BLPOP",  new BlPopCommand()  },
        };

    public static async Task<string> DispatchAsync(string input)
    {
        RespToken[]? tokens = RespParser.Parse(input);

        if (tokens is null || tokens.Length == 0)
            return Resp.InvalidFmt;

        string cmd = tokens[0].Value;

        if (_async.TryGetValue(cmd, out var asyncHandler))
            return await asyncHandler.ExecuteAsync(tokens);

        if (_sync.TryGetValue(cmd, out var handler))
            return handler.Execute(tokens);

        return Resp.Err($"unknown command '{cmd}'");
    }
}
