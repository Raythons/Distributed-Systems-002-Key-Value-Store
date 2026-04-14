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

            // Simple response for now.
            await client.SendAsync("+PONG\r\n"u8.ToArray());
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
