using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Security.Cryptography;
using ChatRouter;
using System.Text.Json;

// This server is limited to messages of up-to 65535 chars (2^16 signed)
public class Server
{
    int connectionCount = 0;
    bool running = true, restarting = false;
    Listener onExit = new Listener();
    TcpListener serverSocket;
    List<Client> clients = new List<Client>();
    //string chatId = "0ccbd809-845a-4d48-939a-68c981ab0f39"; //TODO: Should be sent by the client, not given from the server

    public async Task Run()
    {
        StartListener();

        Task main = Task.Run(async () =>
        {
            while (running)
            {
                try
                {
                    if (!restarting)
                    {
                        var clientSocket = await serverSocket.AcceptTcpClientAsync();
                        connectionCount++;

                        _ = Task.Run(async () => await HandleWebSocketConnection(clientSocket));
                    }
                }
                catch (Exception ex)
                {
                    if (!running)
                    {
                        Logger.Info($"Server closed");
                    }
                    else
                    {
                        Logger.Fatal($"{ex.Message}");
                    }
                }
            }
        });

        while (running)
        {
            string cmd = Console.ReadLine();
            if(cmd == null)
            {
                continue;
            }

            await HandleCommand(cmd);
        }

        await main;
    }

    void StartListener()
    {
        var ipAddress = IPAddress.Any;
        var port = 5201;

        var endPoint = new IPEndPoint(ipAddress, port);
        serverSocket = new TcpListener(endPoint);
        serverSocket.Start();
        Logger.Info($"WebSocket server listening on {endPoint}");
    }

    async Task HandleCommand(string cmd)
    {
        switch (cmd)
        {
            case "exit":
                List<Task> closing = new List<Task>();
                foreach (Client client in clients)
                {
                    closing.Add(client.SendClose());
                }
                Task.WaitAll(closing.ToArray());
                running = false;
                serverSocket.Stop();
                onExit.Invoke();
                break;
            case "restart":
                List<Task> restarts = new List<Task>();
                foreach (Client client in clients)
                {
                    restarts.Add(client.SendClose());
                }
                Task.WaitAll(restarts.ToArray());
                restarting = true;
                serverSocket.Stop();
                onExit.Invoke();
                StartListener();
                restarting = false;
                break;
            case "clear":
                Console.Clear();
                break;
            case "connections":
                Logger.Info($"Current Connections: {connectionCount}");
                break;
        }
    }

    async Task HandleWebSocketConnection(TcpClient clientSocket)
    {
        var stream = clientSocket.GetStream();
        var buffer = new byte[1024];

        // Read the client's handshake request
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        var sessionIdIndex = request.IndexOf("sessionId=") + 10;
        var sessionId = request.Substring(sessionIdIndex, 36);

        // Check SessionId
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Cookie", "sessionId=" + sessionId);
        var result = await httpClient.GetAsync("http://localhost/api/identity/login/valid");
        if (result.StatusCode != HttpStatusCode.OK)
        {
            var responseString = $"HTTP/1.1 403 Forbidden\r\n" +
                                 $"Content-Length: 0\r\n\r\n";
            var errorBytes = Encoding.UTF8.GetBytes(responseString);
            await stream.WriteAsync(errorBytes, 0, errorBytes.Length);
            Logger.Warn($"A connection attempt from: {clientSocket.Client.RemoteEndPoint} was rejected");
            return;
        }

        // Extract the WebSocket key from the request
        var keyStart = request.IndexOf("Sec-WebSocket-Key:") + 19;
        if (keyStart <= 19)
        {
            keyStart = request.IndexOf("sec-websocket-key:") + 19;
        }
        var keyEnd = request.IndexOf("\r\n", keyStart);
        var key = request.Substring(keyStart, keyEnd - keyStart).Trim();

        string acceptKey = GenerateWebSocketAcceptKey(key);

        var response = $"HTTP/1.1 101 Switching Protocols\r\n" +
                       $"Upgrade: websocket\r\n" +
                       $"Connection: Upgrade\r\n" +
                       $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";
        // Accept Connection
        var responseBytes = Encoding.UTF8.GetBytes(response);
        await stream.WriteAsync(responseBytes, 0, responseBytes.Length);

        Client client = new Client(clientSocket, stream, sessionId);

        client.onMessageReceived.AddListener((ServerMessage message) =>
        {
            ForwardMessage(client, message);
        });
        client.onMessageReceived.AddListener(async (ServerMessage message) =>
        {
            try
            {
                await StorageManager.SaveMessage(client, message);
            }
            catch (Exception e)
            {
                Logger.Warn(e.Message);
            }
        });

        clients.Add(client);

        onExit.AddListener(() =>
        {
            if (client != null && client.Close())
            {
                connectionCount--;
                connectionCount = connectionCount < 0 ? 0 : connectionCount;
                if (clients.Contains(client))
                {
                    clients.Remove(client);
                }
            }
        });

        client.Run();

        if (client.Close())
        {
            connectionCount--;
            connectionCount = connectionCount < 0 ? 0 : connectionCount;
            if (clients.Contains(client))
            {
                clients.Remove(client);
            }
        }
    }

    public void ForwardMessage(Client sender, ServerMessage message)
    {
        List<Task> tasks = new List<Task>();
        foreach (Client client in clients)
        {
            if (client == sender)
            {
                continue;
            }

            tasks.Add(client.SendFrame(JsonSerializer.Serialize(message)));
        }
        Task.WaitAll(tasks.ToArray());
    }

    public string GenerateWebSocketAcceptKey(string clientKey)
    {
        // Concatenate the client's key with the magic string
        string magicString = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        string combinedKey = clientKey + magicString;

        // Calculate the SHA-1 hash of the combined key
        using (SHA1 sha1 = SHA1.Create())
        {
            byte[] hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(combinedKey));

            // Encode the hash in base64
            string base64Encoded = Convert.ToBase64String(hashBytes);
            return base64Encoded;
        }
    }
}
