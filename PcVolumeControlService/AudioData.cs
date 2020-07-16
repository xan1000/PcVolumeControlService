using System.Collections.Generic;
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable CollectionNeverQueried.Global

namespace VolumeControl
{
    public class PcAudio
    {
        public int ProtocolVersion { get; set; }
        public string ApplicationVersion { get; set; }
        public IDictionary<string, string> DeviceIds { get; } = new Dictionary<string, string>();
        public AudioDevice DefaultDevice { get; set; }
    }

    public class AudioDevice
    {
        public string DeviceId { get; set; }
        public string Name { get; set; }
        public double? MasterVolume { get; set; }
        public bool? MasterMuted { get; set; }
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
