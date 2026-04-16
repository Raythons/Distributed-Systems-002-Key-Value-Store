
public class RedisString : RedisValue
{
    public override string TypeName => "string";

    public string Value { get; private set; }

    public RedisString(string value)
    {
        Value = value;
    }

    public void Set(string value) => Value = value;
}
