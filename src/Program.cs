using System.Net;
using System.Net.Sockets;

// bind(fd=5, port=6379)
TcpListener server = new TcpListener(IPAddress.Any, 6379);
server.Start(); // listen(fd=5, backlog)

Console.WriteLine("Redis server started on port 6379...");

while (true)
{
    // accept(fd=5) -> returns fd=6, fd=7, etc.
    // This is the "dispatcher". It waits for new connections.
    Socket client = await server.AcceptSocketAsync();
    
    Console.WriteLine($"[Main Loop] Accepted new connection: fd={client.Handle}");

    // Discard (_) = Fire-and-forget. 
    // We don't await this, so we can immediately accept the next client.
    _ = HandleConnection(client);
}

async Task HandleConnection(Socket client)
{
    byte[] buffer = new byte[1024];

    try
    {
        // One handler per client, running in its own loop.
        while (true)
        {
            // Suspends here until data arrives. Does NOT return 0 unless FIN received.
            int bytesRead = await client.ReceiveAsync(buffer);

            if (bytesRead == 0)
            {
                Console.WriteLine($"[Handler {client.Handle}] Client disconnected gracefully.");
                break;
            }

            string received = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Console.WriteLine($"[Handler {client.Handle}] Received: {received.TrimEnd()}");

            string response = CommandDispatcher.Dispatch(received);
            await client.SendAsync(System.Text.Encoding.UTF8.GetBytes(response));
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Handler {client.Handle}] Error: {ex.Message}");
    }
    finally
    {
        // CRITICAL: Release the OS File Descriptor
        client.Close();
        Console.WriteLine($"[Handler] Socket closed.");
    }
}

// A RESP bulk string: its declared length + its actual value
public record struct RespToken(int DeclaredLength, string Value);

// ============================================================
// Layer 1: RESP Parser — dumb, knows NOTHING about commands.
// Only job: turn raw RESP wire string into a clean RespToken[].
// Also here its like the compilers Parser  it parse the  incoming string to Lex it later via Lexer :) 
// ============================================================
public static class RespParser
{
    public static RespToken[]? Parse(string input)
    {
        var parts = input.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        // Must start with '*' 
        if (parts.Length == 0 || !parts[0].StartsWith("*"))
            return null;

        var tokens = new List<RespToken>();

        // Tokens come in pairs: "$length" then "value" 
        for (int i = 1; i + 1 < parts.Length; i += 2)
        {
            if (parts[i].StartsWith("$") &&
                int.TryParse(parts[i][1..], out int declaredLen))
            {
                string value = parts[i + 1];

                // Validate: declared length must match actual length
                if (value.Length != declaredLen)
                {
                    Console.WriteLine($"[WARN] RESP length mismatch: declared={declaredLen}, actual={value.Length}");
                }

                tokens.Add(new RespToken(declaredLen, value));
            }
        }

        return tokens.Count > 0 ? tokens.ToArray() : null;
    }
}


public static class CommandDispatcher
{
    public static string Dispatch(string input)
    {
        RespToken[]? tokens = RespParser.Parse(input);

        if (tokens is null || tokens.Length == 0)
            return "-ERR invalid format\r\n";

        return tokens[0].Value.ToUpper() switch
        {
            "PING" => "+PONG\r\n",
            "ECHO" => HandleEcho(tokens),
            // "SET"  => HandleSet(tokens),
            var cmd => $"-ERR unknown command '{cmd}'\r\n"
        };
    }

    // ----------------------------------------------------------
    // ECHO handler
    // tokens[0] = "ECHO", tokens[1] = "hello"
    // ----------------------------------------------------------
    private static string HandleEcho(RespToken[] tokens)
    {
        if (tokens.Length < 2)
            return "-ERR wrong number of arguments for 'echo'\r\n";

        string arg = tokens[1].Value;
        int declaredLength = tokens[1].DeclaredLength;
        // Alternatively, use our newly captured DeclaredLength!
        return $"${declaredLength}\r\n{arg}\r\n";
    }

    // ----------------------------------------------------------
    // SET handler — owns its own flag parsing (EX, PX, NX, XX)
    // tokens[0] = "SET", ...
    // ----------------------------------------------------------
    private static string HandleSet(RespToken[] tokens)
    {
        if (tokens.Length < 3)
            return "-ERR wrong number of arguments for 'set'\r\n";

        string key   = tokens[1].Value;
        string value = tokens[2].Value;

        // Optional flags — only this handler needs to know about them
        TimeSpan? expiry = null;
        bool onlyIfNotExists = false;
        bool onlyIfExists    = false;

        for (int i = 3; i < tokens.Length; i++)
        {
            switch (tokens[i].Value.ToUpper())
            {
                case "EX": expiry = TimeSpan.FromSeconds(double.Parse(tokens[++i].Value)); break;
                case "PX": expiry = TimeSpan.FromMilliseconds(double.Parse(tokens[++i].Value)); break;
                case "NX": onlyIfNotExists = true; break;
                case "XX": onlyIfExists    = true; break;
            }
        }

        Console.WriteLine($"[SET] key={key} value={value} expiry={expiry} NX={onlyIfNotExists} XX={onlyIfExists}");

        // TODO: plug in actual store logic here
        return "+OK\r\n";
    }
}
