using System.Net;
using System.Net.Sockets;

// bind(fd=5, port=6379)
TcpListener server = new TcpListener(IPAddress.Any, 6379);
server.Start(); // listen(fd=5, backlog)

Console.WriteLine("Redis server started on port 6379...");

// Start the background active expirer — runs alongside the server forever
_ = Store.StartActiveExpirerAsync();

while (true)
{
    // accept(fd=5) -> returns fd=6, fd=7, etc.
    Socket client = await server.AcceptSocketAsync();

    Console.WriteLine($"[Main Loop] Accepted new connection: fd={client.Handle}");

    // Fire-and-forget: don't await so we can accept the next client immediately
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
            // Suspends here until data arrives. Returns 0 only on FIN (disconnect).
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


