public class LPushCommand : ICommandHandler
{
    public string Name => "LPUSH";

    public string Execute(RespToken[] tokens)
    {
        if (tokens.Length < 3)
            return Resp.Err("wrong number of arguments for 'lpush'");

        string key = tokens[1].Value;
        
        // Extract all elements to append
        string[] elements = new string[tokens.Length - 2];
        for (int i = 0; i < elements.Length; i++)
        {
            elements[i] = tokens[i + 2].Value;
        }

        var (length, error) = Store.LPush(key, elements);

        if (error != null)
            return error;

        return $":{length}\r\n"; // RESP integer
    }
}
