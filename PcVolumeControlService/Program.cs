namespace PcVolumeControlService;

public static class Program
{
    public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args).
            ConfigureLogging((context, logging) =>
            {
                if(context.HostingEnvironment.IsProduction() &&
                    context.Configuration.GetValue<bool>("LoggingEnabled"))
                    logging.AddFile(context.Configuration.GetSection("Logging"));
            }).
            ConfigureServices(services =>
            {
                services.AddSingleton<IClient, Client>();
                services.AddSingleton<CachingCoreAudioController>();
                services.AddHostedService<Server>();
                services.AddHostedService<WarmUp>();
            }).UseWindowsService();
}
