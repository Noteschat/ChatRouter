class Program
{
    static async Task Main()
    {
        Server serv = new Server();
        await serv.Run();
    }
}
