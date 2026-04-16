public class GetCommand : ICommandHandler
{
    public string Name => "GET";

    public string Execute(RespToken[] tokens)
    {
        if (tokens.Length < 2)
            return Resp.Err("wrong number of arguments for 'get'");

        var (value, error) = Store.Get(tokens[1].Value);

        if (error != null)
            return error;

        if (value is null)
            return Resp.NilBulk;

        return Resp.Bulk(value);
    }
}
