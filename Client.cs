using ChatRouter;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

public class Client
{
    TcpClient socket;
    NetworkStream stream;

    bool wasClosed = false, dead = false;
    public string sessionId { get; private set; } = "";

    public Listener<ServerMessage> onMessageReceived
    {
        get;
        private set;
    } = new Listener<ServerMessage>();

    public Client (TcpClient socket, NetworkStream stream, string sessionId)
    {
        this.socket = socket;
        this.stream = stream;
        this.sessionId = sessionId;
    }

    public void Run()
    {
        var dead = false;
        List<Task> tasks = new List<Task>()
        {
            Task.Run(async () =>
            {
                while (!dead && !wasClosed)
                {
                    Frame? frame = await GetFrame();
                    if(!frame.HasValue || frame.Value.AllEmpty || frame.Value.opCode == OpCode.Close)
                    {
                        dead = true;
                    }
                }
            })
        };

        Logger.Info("New Client connected.");

        Task.WaitAll(tasks.ToArray());
    }

    public async Task SendFrame(string message)
    {
        try
        {
            var byteMessage = Encoding.UTF8.GetBytes(message);
            var frame = new byte[byteMessage.Length + (byteMessage.Length > 125 ? 4 : 2)];
            frame[0] = 0x81; // FIN bit set, opcode for text frame
            int index = 2;
            if(byteMessage.Length > 125)
            {
                frame[1] = 126;
                frame[3] = (byte)(byteMessage.Length & 0xFF);       // Lower 8 bits
                frame[2] = (byte)((byteMessage.Length >> 8) & 0xFF); // Upper 8 bits
                index = 4;
            }
            else
            {
                frame[1] = (byte)byteMessage.Length;
            }
            Array.Copy(byteMessage, 0, frame, index, byteMessage.Length);

            // Send the frame to the client
            await stream.WriteAsync(frame, 0, frame.Length);
        }
        catch (Exception e)
        {
            Logger.Error("Couldn't send message!");
            Logger.Error(e.Message);
        }
    }

    public async Task<Frame?> GetFrame(bool subCall = false)
    {
        var receiveBuffer = new byte[1024];
        var result = 0;
        try
        {
            result = await stream.ReadAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
        }
        catch
        {
            return null;
        }

        Frame frame = await Frame.convertToFrame(result, receiveBuffer, stream);

        if(frame.opCode == OpCode.Pong)
        {
            await SendPing();
            return null;
        }

        if (frame.opCode == OpCode.Ping)
        {
            await SendPong();
            return null;
        }

        if (!frame.isValid())
        {
            return null;
        }

        if (frame.payloadLength <= 0)
        {
            Logger.Warn($"Got Empty Frame:");
            return null;
        }

        try
        {
            var receivedMessage = Encoding.UTF8.GetString(frame.payload);
            onMessageReceived.Invoke(JsonSerializer.Deserialize<ServerMessage>(receivedMessage));
        }
        catch (Exception ex)
        {
            Logger.Error($"Decoding:\n{ex}");
        }

        return frame;
    }

    public async Task SendPing()
    {
        try
        {
            var frame = new byte[2];
            frame[0] = 137;

            await stream.WriteAsync(frame, 0, frame.Length);
        }
        catch (Exception e)
        {
            Logger.Error("Couldn't send ping!");
            Logger.Error(e.Message);
        }
    }

    public async Task SendPong()
    {
        try
        {
            var frame = new byte[2];
            frame[0] = 138;

            await stream.WriteAsync(frame, 0, frame.Length);
        }
        catch (Exception e)
        {
            Logger.Error("Couldn't send ping!");
            Logger.Error(e.Message);
        }
    }

    public async Task SendClose()
    {
        try
        {
            var frame = new byte[2];
            frame[0] = 136;

            await stream.WriteAsync(frame, 0, frame.Length);
        }
        catch (Exception e)
        {
            Logger.Error("Couldn't send close!");
            Logger.Error(e.Message);
        }
    }

    public bool Close()
    {
        if (!wasClosed)
        {
            Logger.Info("Client disconnected.");

            socket.Close();
            stream.Close();

            wasClosed = true;

            return true;
        }

        return false;
    }
}