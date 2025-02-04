﻿using System.Net.Sockets;
using System.Text;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PcVolumeControlService;

public class Client(ILogger<Client> logger, CachingCoreAudioController cachingCoreAudioController) : IClient
{
    private const string ApplicationVersion = "v8";
    private const int ProtocolVersion = 7;

    private static readonly Encoding Encoding = Encoding.ASCII;

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };

    private readonly ILogger<Client> _logger = logger;
    private readonly CachingCoreAudioController _cachingCoreAudioController = cachingCoreAudioController;

    public async Task ExecuteAsync(TcpClient tcpClient, CancellationToken stoppingToken)
    {
        _logger.LogTrace("Client started at: {time}", DateTime.Now);

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
                _logger.LogTrace("Reading message from client.");
                var message = await streamReader.ReadLineAsync(stoppingToken);
                if(message == null)
                    break;

                _logger.LogTrace("Reading complete, deserialising message from client.");
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
            
        _logger.LogTrace("Client ended at: {time}", DateTime.Now);
    }

    private async Task SendCurrentAudioStateAsync(
        StreamWriter streamWriter, CoreAudioController coreAudioController, CancellationToken stoppingToken)
    {
        _logger.LogTrace("Sending current audio state to client.");

        var audioState = await GetCurrentAudioStateAsync(coreAudioController);

        var json = JsonConvert.SerializeObject(audioState, JsonSettings);

        await streamWriter.WriteLineAsync(new StringBuilder(json), stoppingToken);
        await streamWriter.FlushAsync(stoppingToken);
    }

    private static async Task<PcAudio> GetCurrentAudioStateAsync(CoreAudioController coreAudioController)
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

    private static async Task UpdateStateAsync(
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
            var deviceAudioVolume = defaultPlaybackDevice.Volume;
            var clientAudioVolume = audioUpdate.DefaultDevice.MasterVolume.Value;

            // Determine the volume value to set.
            double volume;
            if(clientAudioVolume < deviceAudioVolume)
            {
                volume = Math.Ceiling(clientAudioVolume);

                // If volume is odd make it even.
                if((int) volume % 2 == 1)
                    volume--;
            }
            else if(clientAudioVolume > deviceAudioVolume)
            {
                // When increasing the volume the increase is always performed in increments of 2.
                const int MaximumIncrement = 2;

                // Increment the volume based on the current device volume level.
                volume = Math.Ceiling(deviceAudioVolume) + MaximumIncrement;
            }
            // This else means the volume levels are equal, there is no need to change the volume.
            else
                return;

            // If the volume is odd then make it even.
            if((int) volume % 2 == 1)
                volume--;

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if(volume != deviceAudioVolume)
                await defaultPlaybackDevice.SetVolumeAsync(volume, stoppingToken);
        }
    }
}
