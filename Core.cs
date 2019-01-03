using Lava.Net.Sources;
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
            SocketServer = new LavaSocketServer("http://"+LavaConfig.SERVER_URI+"/");
        }

        public async Task MainAsync()
        {
            await SocketServer.StartAsync();
            await Task.Delay(-1);
        }
    }
}
