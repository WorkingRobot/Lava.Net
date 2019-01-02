using Lava.Net.Sources;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Lava.Net
{
    public class Core
    {
        LavaSocketServer SocketServer;

        public static void Main(string[] args) => new Core().MainAsync().GetAwaiter().GetResult();

        public Core()
        {
            //Console.WriteLine(string.Join("\n\n\n\n",Youtube.Search("fortnite stream").GetAwaiter().GetResult().Select(t=>JsonConvert.SerializeObject(t))));
            SocketServer = new LavaSocketServer("http://"+LavaConfig.SERVER_URI+"/");
        }

        public async Task MainAsync()
        {
            await SocketServer.StartAsync();
            await Task.Delay(1000000000);
        }
    }
}
