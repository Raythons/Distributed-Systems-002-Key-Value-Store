public class SetCommand : ICommandHandler
{
    public string Name => "SET";

    public string Execute(RespToken[] tokens)
    {
        if (tokens.Length < 3)
            return Resp.Err("wrong number of arguments for 'set'");

        string key   = tokens[1].Value;
        string value = tokens[2].Value;

        TimeSpan? ttl = null;

        for (int i = 3; i < tokens.Length; i++)
        {
            switch (tokens[i].Value.ToUpper())
            {
                case "EX" when i + 1 < tokens.Length:
                    ttl = TimeSpan.FromSeconds(double.Parse(tokens[++i].Value));
                    break;
                case "PX" when i + 1 < tokens.Length:
                    ttl = TimeSpan.FromMilliseconds(double.Parse(tokens[++i].Value));
                    break;
            }
        }

        Store.Set(key, value, ttl);
        Console.WriteLine($"[SET] key='{key}' value='{value}' ttl={ttl}");
        return Resp.Ok;
    }
}
