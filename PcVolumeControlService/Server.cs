using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PcVolumeControlService
{
    public class Server : BackgroundService
    {
        private const int Port = 3000;

        private readonly ILogger<Server> _logger;
        private readonly IClient _client;

        public Server(ILogger<Server> logger, IClient client)
        {
            _logger = logger;
            _client = client;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Server running at: {time}", DateTime.Now);

            var tcpListener = new TcpListener(IPAddress.Any, Port);
            tcpListener.Start();

            await using(stoppingToken.Register(tcpListener.Stop))
            {
                try
                {
                    while(!stoppingToken.IsCancellationRequested)
                    {
                        var tcpClient = await tcpListener.AcceptTcpClientAsync();
                        RunClient(tcpClient, stoppingToken);
                    }
                }
                catch(Exception e)
                {
                    // https://devblogs.microsoft.com/pfxteam/how-do-i-cancel-non-cancelable-async-operations/
                    // Either tcpListener.Start wasn't called
                    // or the CancellationToken was cancelled before
                    // we started accepting (giving an InvalidOperationException),
                    // or the CancellationToken was cancelled after
                    // we started accepting (giving an ObjectDisposedException).
                    if(!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogError(e, $"Exception occurred and {nameof(stoppingToken)} was not cancelled.");
                        throw;
                    }
                }
            }

            _logger.LogInformation("Server stopping at: {time}", DateTime.Now);
        }

        private void RunClient(TcpClient tcpClient, CancellationToken stoppingToken)
        {
            // Run client in the background.
            Task.Run(async () =>
            {
                using(tcpClient)
                {
                    await using(stoppingToken.Register(tcpClient.Dispose))
                    {
                        await _client.ExecuteAsync(tcpClient, stoppingToken);
                    }
                }
            }, stoppingToken).ConfigureAwait(false);
        }
    }
}
