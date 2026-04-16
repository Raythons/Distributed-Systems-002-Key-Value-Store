public class LlenCommand : ICommandHandler {
    public string Name => "LLEN";

    public string Execute(RespToken[] tokens)
    {
        if (tokens.Length != 2)
            return Resp.Err("wrong number of arguments for 'llen'");

        var (length, error) = Store.LLen(tokens[1].Value);

        if (error != null)
            return error;

        return $":{length}\r\n";
    }
}
