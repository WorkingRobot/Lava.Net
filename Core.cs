using Lava.Net.Sources.Youtube;
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
            Console.Title = "Lava.Net v0.1";
            //Console.WriteLine(Youtube.GetStream("D7dlT6VRsdw").GetAwaiter().GetResult());
            SocketServer = new LavaSocketServer("http://"+LavaConfig.SERVER_URI+"/");
        }

        public async Task MainAsync()
        {
            await SocketServer.StartAsync();
            await Task.Delay(-1);
        }
    }
}
