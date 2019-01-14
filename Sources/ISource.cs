using Lava.Net.Streams;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lava.Net.Sources
{
    interface ISource
    {
        bool ValidateTrack(string identifier);

        Task<(List<LavaTrack> tracks, LoadType type)> Search(string query);

        Task<(LavaTrack track, LoadType type)> GetTrack(string identifier);

        Task<LavaStream> GetStream(LavaTrack track);

        Task<LavaStream> GetStream(string identifier);
    }
}
