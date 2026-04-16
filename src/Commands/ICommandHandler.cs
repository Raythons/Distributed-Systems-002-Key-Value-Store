// Strategy interface — every command implements this.
public interface ICommandHandler
{
    string Name { get; }
    string Execute(RespToken[] tokens);
}
