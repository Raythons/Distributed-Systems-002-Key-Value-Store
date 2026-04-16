public class EchoCommand : ICommandHandler
{
    public string Name => "ECHO";

    public string Execute(RespToken[] tokens)
    {
        if (tokens.Length < 2)
            return Resp.Err("wrong number of arguments for 'echo'");

        return Resp.Bulk(tokens[1].Value);
    }
}
