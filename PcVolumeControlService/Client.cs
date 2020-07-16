using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using VolumeControl;

namespace PcVolumeControlService
{
    public class Client : IClient
    {
        private const string ApplicationVersion = "v8";
        private const int ProtocolVersion = 7;

        private static readonly Encoding Encoding = Encoding.ASCII;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        private readonly ILogger<Client> _logger;
        private readonly CachingCoreAudioController _cachingCoreAudioController;

        public Client(ILogger<Client> logger, CachingCoreAudioController cachingCoreAudioController)
        {
            _logger = logger;
            _cachingCoreAudioController = cachingCoreAudioController;
        }

        public async Task ExecuteAsync(TcpClient tcpClient, CancellationToken stoppingToken)
        {
            try
            {
                await using var bufferedStream = new BufferedStream(tcpClient.GetStream());
                using var streamReader = new StreamReader(bufferedStream, Encoding);
                await using var streamWriter = new StreamWriter(bufferedStream, Encoding);

                if(tcpClient.Connected)
                {
                    var coreAudioController = _cachingCoreAudioController.GetCoreAudioController(stoppingToken);
                    await SendCurrentAudioStateAsync(streamWriter, coreAudioController, stoppingToken);
                }

                while(tcpClient.Connected)
                {
                    var message = await streamReader.ReadLineAsync();
                    if(message == null)
                        return;

                    var audioUpdate = JsonConvert.DeserializeObject<PcAudio>(message, JsonSettings);
                    if(audioUpdate!.ProtocolVersion != ProtocolVersion)
                        throw new InvalidOperationException(
                            $"Protocol version mismatch Client('{audioUpdate.ProtocolVersion}') != " +
                            $"Server('{ProtocolVersion}')");

                    var coreAudioController = _cachingCoreAudioController.GetCoreAudioController(stoppingToken);
                    await UpdateStateAsync(audioUpdate, coreAudioController, stoppingToken);
                    await SendCurrentAudioStateAsync(streamWriter, coreAudioController, stoppingToken);
                }
            }
            catch(Exception e)
            {
                if(!stoppingToken.IsCancellationRequested)
                    _logger.LogError(e, $"Exception occurred for client and {nameof(stoppingToken)} was not cancelled.");
            }
        }

        private async Task SendCurrentAudioStateAsync(
            StreamWriter streamWriter, CoreAudioController coreAudioController, CancellationToken stoppingToken)
        {
            var audioState = await GetCurrentAudioStateAsync(coreAudioController);

            var json = JsonConvert.SerializeObject(audioState, JsonSettings);

            await streamWriter.WriteLineAsync(new StringBuilder(json), stoppingToken);
            await streamWriter.FlushAsync();
        }

        private async Task<PcAudio> GetCurrentAudioStateAsync(CoreAudioController coreAudioController)
        {
            var audioState = new PcAudio
            {
                ApplicationVersion = ApplicationVersion,
                ProtocolVersion = ProtocolVersion
            };

            var devices = await coreAudioController.GetPlaybackDevicesAsync();
            foreach(var device in devices.Where(x => x.State == DeviceState.Active).ToList())
            {
                audioState.DeviceIds.Add(device.Id.ToString(), device.FullName);
            }

            var defaultPlaybackDevice = coreAudioController.DefaultPlaybackDevice;

            audioState.DefaultDevice = new AudioDevice
            {
                Name = defaultPlaybackDevice.FullName,
                DeviceId = defaultPlaybackDevice.Id.ToString(),
                MasterVolume = defaultPlaybackDevice.Volume,
                MasterMuted = defaultPlaybackDevice.IsMuted
            };

            return audioState;
        }

        private async Task UpdateStateAsync(
            PcAudio audioUpdate, CoreAudioController coreAudioController, CancellationToken stoppingToken)
        {
            if(audioUpdate?.DefaultDevice == null)
                return;

            var defaultPlaybackDevice = coreAudioController.DefaultPlaybackDevice;

            // Change default audio device.
            if(audioUpdate.DefaultDevice.DeviceId != defaultPlaybackDevice.Id.ToString())
            {
                var deviceId = Guid.Parse(audioUpdate.DefaultDevice.DeviceId);
                var newDefaultAudioDevice = await coreAudioController.GetDeviceAsync(deviceId);

                await newDefaultAudioDevice.SetAsDefaultAsync(stoppingToken);
                await newDefaultAudioDevice.SetAsDefaultCommunicationsAsync(stoppingToken);

                return;
            }

            // Change muted and / or volume values.
            if(audioUpdate.DefaultDevice.MasterMuted.HasValue)
            {
                var muted = audioUpdate.DefaultDevice.MasterMuted.Value;

                if(muted != defaultPlaybackDevice.IsMuted)
                    await defaultPlaybackDevice.SetMuteAsync(muted, stoppingToken);
            }
            if(audioUpdate.DefaultDevice.MasterVolume.HasValue)
            {
                const int increment = 2;

                var deviceAudioVolume = defaultPlaybackDevice.Volume;
                var clientAudioVolume = audioUpdate.DefaultDevice.MasterVolume.Value;

                var volume = deviceAudioVolume;
                if(clientAudioVolume < deviceAudioVolume)
                    volume -= increment;
                else if(clientAudioVolume > deviceAudioVolume)
                    volume += increment;

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if(volume != deviceAudioVolume)
                    await defaultPlaybackDevice.SetVolumeAsync(volume, stoppingToken);
            }
        }
    }
}
