namespace PcVolumeControlService;

public class WarmUp(CachingCoreAudioController cachingCoreAudioController) : BackgroundService
{
    private readonly CachingCoreAudioController _cachingCoreAudioController = cachingCoreAudioController;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Perform an initial loading of Core Audio Controller to warm up the application.
        // The Core Audio Controller library can take some time to return and is blocking.
        await Task.Run(() => _cachingCoreAudioController.GetCoreAudioController(stoppingToken), stoppingToken);
    }
}
