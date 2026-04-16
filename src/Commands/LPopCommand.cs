public class LPopCommand : ICommandHandler
{
    public string Name => "LPOP";

    public string Execute(RespToken[] tokens)
    {
        if (tokens.Length < 2)
            return Resp.Err("wrong number of arguments for 'lpop'");

        string key = tokens[1].Value;
        
        bool hasCount = tokens.Length > 2;
        int count = 1;

        if (hasCount)
        {
            if (!int.TryParse(tokens[2].Value, out count) || count < 0)
                return Resp.Err("value is not an integer or out of range");
        }

        var (popped, error) = Store.LPop(key, count);

        if (error != null)
            return error;

        if (popped == null || popped.Count == 0)
            return Resp.NilBulk;

        if (hasCount)
        {
            return Resp.Array(popped);
        }
        else
        {
            // If the user called `LPOP key`, return a single Bulk String
            return Resp.Bulk(popped[0]);
        }
    }
}
