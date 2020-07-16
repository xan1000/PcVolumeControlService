using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PcVolumeControlService
{
    public static class Program
    {
        public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

        private static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args).
            ConfigureLogging((context, logging) => logging.AddFile(context.Configuration.GetSection("Logging"))).
            ConfigureServices(services =>
            {
                services.AddSingleton<IClient, Client>();
                services.AddSingleton<CachingCoreAudioController>();
                services.AddHostedService<Worker>();
            }).UseWindowsService();
    }
}
