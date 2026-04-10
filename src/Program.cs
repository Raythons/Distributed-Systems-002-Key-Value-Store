using System.Net;
using System.Net.Sockets;
using System.Text;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

TcpListener server = new TcpListener(IPAddress.Any, 6379);
server.Start();

// AcceptSocket() blocks here until a client connects, then returns the connected socket
Socket client = server.AcceptSocket();

// Read what the client sent (e.g. the PING command)
byte[] buffer = new byte[1024];
client.Receive(buffer);

// Send back the PONG response
client.Send(Encoding.UTF8.GetBytes("+PONG\r\n"));