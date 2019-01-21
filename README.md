# Lava.Net [![Build status](https://ci.appveyor.com/api/projects/status/q3v8ygegrjdx0fb7?svg=true)](https://ci.appveyor.com/project/WorkingRobot/lava-net)

Lava.Net is a (.NET Core 2.0) drop-in replacement for [Lavalink](https://github.com/Frederikam/Lavalink), supporting the full API set. Any existing Lavalink-compatible library will work perfectly with Lava.Net.

To Do:
 - [x] Make a REST and WebSocket connection available
 - [x] Make `/loadtracks` REST endpoint available.
 - [x] Add YouTube searching
 - [x] Connect to voice via UDP
 - [x] Add ability to send opus encoded voice
 - [x] Get and send YouTube stream to voice
 - [x] Allow playing with custom start and end time (Note: A lot of Lavalink sources don't actually seek for some reason, like Youtube or Soundcloud)
 - [x] Enable pausing, stopping, and destroy payloads
 - [x] Enable seeking and volume payloads
 - [ ] Send player update payload
 - [x] Send stats payload
 - [ ] Send events
 - [x] Enable equalizer payload
 - [x] Allow SoundCloud searching and streaming
 - [ ] Allow local and web (HTTP) streaming
 - [ ] Implement a better streaming system for URLs (less weird errors)
 - [ ] More will be added as the list progresses
