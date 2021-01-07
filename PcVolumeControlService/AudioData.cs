using System.Collections.Generic;
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable CollectionNeverQueried.Global

namespace VolumeControl
{
    public class PcAudio
    {
        public int ProtocolVersion { get; init; }
        public string ApplicationVersion { get; init; }
        public IDictionary<string, string> DeviceIds { get; } = new Dictionary<string, string>();
        public AudioDevice DefaultDevice { get; set; }
    }

    public class AudioDevice
    {
        public string DeviceId { get; init; }
        public string Name { get; init; }
        public double? MasterVolume { get; init; }
        public bool? MasterMuted { get; init; }
        public IList<AudioSession> Sessions { get; } = new List<AudioSession>();
    }

    public class AudioSession
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public double Volume { get; set; }
        public bool Muted { get; set; }
    }
}
