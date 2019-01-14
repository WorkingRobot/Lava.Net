using System;
using System.Threading.Tasks;

namespace Lava.Net
{
    public class Core
    {
        LavaSocketServer SocketServer;

        public static void Main(string[] args) => new Core().MainAsync().GetAwaiter().GetResult();

        public Core()
        {
            Console.Title = "Lava.Net v0.2";
            SocketServer = new LavaSocketServer("http://"+LavaConfig.Server.Uri+"/");
        }

        public async Task MainAsync()
        {
            await SocketServer.StartAsync();
            await Task.Delay(-1);
        }
    }
}
