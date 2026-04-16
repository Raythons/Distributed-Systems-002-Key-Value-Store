using System.Security.Cryptography.X509Certificates;

public class LRangeCommand : ICommandHandler
{
    public string Name => "LRANGE";

    public string Execute(RespToken[] tokens)
    {
        if (tokens.Length < 4)
            return Resp.Err("wrong number of arguments for 'lrange'");

        string key = tokens[1].Value;
        int start = int.Parse(tokens[2].Value);
        int stop = int.Parse(tokens[3].Value);

        var (list, error) = Store.GetList(key);
        if (error != null)
            return error;
        
        if (list == null)
            return Resp.EmptyArray; // Empty array if key doesn't exist

        var range = list.GetRange(start, stop);
        return Resp.Array(range);
    }
}
