using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Lava.Net
{
    /*{"playingPlayers":1,"op":"stats","memory":{"reservable":4286578688,"used":92229728,"free":230731680,"allocated":322961408},"players":1,"cpu":{"cores":8,"systemLoad":0.05115509832611897,"lavalinkLoad":0.004112964366944655},"uptime":68325}*/
    /*{"op":"playerUpdate","state":{"position":139660,"time":1547968814823},"guildId":"512156811206590475"}*/
    class StatsCollector
    {
        public readonly LavaSocketServer Server;
        Process CurrentProcess;

        DateTime LastTiming;
        long LastProcessTiming;
        long SentFrames;
        long NullFrames;
        long DeficitFrames;

        public StatsCollector(LavaSocketServer server)
        {
            CurrentProcess = Process.GetCurrentProcess();
            Server = server;
        }

        public LavaStats GetStats()
        {
            long vramsize = CurrentProcess.VirtualMemorySize64;
            long usedramsize = GC.GetTotalMemory(true);
            var timings = GetTimings();
            return new LavaStats()
            {
                Players = Server.Connections.Select(conn => conn.Connections.Count).Sum(),
                PlayingPlayers = Server.Connections.Select(conn => conn.Connections.Count(guildConn => guildConn.Value.Playing)).Sum(),
                Cpu = new LavaStats.CpuStats()
                {
                    Cores = Environment.ProcessorCount,
                    LavalinkLoad = timings.ProcessTiming,
                    SystemLoad = 0 // Don't know how to yet
                },
                Memory = new LavaStats.MemoryStats()
                {
                    Allocated = vramsize,
                    Used = usedramsize,
                    Free = 0, // .NET doesn't allocate RAM ahead of time like Java
                    Reservable = 0 // .NET doesn't have a maximum limit like Java
                },
                Frames = new LavaStats.FrameStats()
                {
                    Sent = timings.AvgSent,
                    Nulled = timings.AvgNull,
                    Deficit = timings.AvgDeficit
                },
                Uptime = (ulong)DateTime.Now.Subtract(CurrentProcess.StartTime).TotalMilliseconds
            };
        }

        (double ProcessTiming, int AvgSent, int AvgNull, int AvgDeficit) GetTimings()
        {
            long ProcessTiming = CurrentProcess.TotalProcessorTime.Ticks;
            DateTime NewTiming = DateTime.Now;
            var timeDelta = NewTiming - LastTiming;

            var ret = ((double)(ProcessTiming - LastProcessTiming) / timeDelta.Ticks, (int)(Interlocked.Read(ref SentFrames) / timeDelta.TotalMinutes), (int)(Interlocked.Read(ref NullFrames) / timeDelta.TotalMinutes), (int)(Interlocked.Read(ref DeficitFrames) / timeDelta.TotalMinutes));
            Interlocked.Exchange(ref SentFrames, 0);
            Interlocked.Exchange(ref NullFrames, 0);
            Interlocked.Exchange(ref DeficitFrames, 0);
            LastTiming = NewTiming;
            LastProcessTiming = ProcessTiming;
            return ret;
        }

        public void IncrementFrame(FrameType type)
        {
            switch (type)
            {
                case FrameType.FULL:
                    Interlocked.Increment(ref SentFrames);
                    return;
                case FrameType.NULL:
                    Interlocked.Increment(ref NullFrames);
                    return;
                case FrameType.DEFICIT:
                    Interlocked.Increment(ref DeficitFrames);
                    return;
            }
        }
    }

    public enum FrameType
    {
        FULL,
        NULL,
        DEFICIT
    }

    public struct LavaStats
    {
        [JsonProperty("op")]
        public const string Op = "stats";

        [JsonProperty("players")]
        public int Players;

        [JsonProperty("playingPlayers")]
        public int PlayingPlayers;

        [JsonProperty("cpu")]
        public CpuStats Cpu;

        [JsonProperty("memory")]
        public MemoryStats Memory;

        [JsonProperty("frameStats", NullValueHandling = NullValueHandling.Ignore)]
        public FrameStats Frames;

        [JsonProperty("uptime")]
        public ulong Uptime;

        public struct MemoryStats
        {
            [JsonProperty("reservable")]
            public long Reservable;

            [JsonProperty("used")]
            public long Used;

            [JsonProperty("free")]
            public long Free;

            [JsonProperty("allocated")]
            public long Allocated;
        }

        public struct CpuStats
        {
            [JsonProperty("cores")]
            public int Cores;

            [JsonProperty("systemLoad")]
            public double SystemLoad;

            [JsonProperty("lavalinkLoad")]
            public double LavalinkLoad;
        }

        public struct FrameStats
        {
            [JsonProperty("sent")]
            public int Sent;

            [JsonProperty("nulled")]
            public int Nulled;

            [JsonProperty("deficit")]
            public int Deficit;
        }
    }
}
