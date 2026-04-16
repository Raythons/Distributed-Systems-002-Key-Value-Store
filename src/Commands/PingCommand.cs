public class PingCommand : ICommandHandler
{
    public string Name => "PING";

    public string Execute(RespToken[] tokens) => Resp.Pong;
}
