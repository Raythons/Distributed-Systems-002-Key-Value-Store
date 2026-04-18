// ============================================================
// IAsyncCommandHandler — for commands that must await I/O
// (e.g. BLPOP blocks until data arrives or timeout fires).
// ============================================================
public interface IAsyncCommandHandler
{
    string Name { get; }
    Task<string> ExecuteAsync(RespToken[] tokens);
}
