using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PcVolumeControlService
{
    public class Worker : BackgroundService
    {
        private const int Port = 3000;

        private readonly ILogger<Worker> _logger;
        private readonly IClient _client;
        private TcpClient _tcpClient;

        public Worker(ILogger<Worker> logger, IClient client)
        {
            _logger = logger;
            _client = client;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker running at: {time}", DateTime.Now);

            var tcpListener = new TcpListener(IPAddress.Any, Port);
            tcpListener.Start();

            await using(stoppingToken.Register(() => tcpListener.Stop()))
            await using(stoppingToken.Register(() => _tcpClient?.Dispose()))
            {
                try
                {
                    while(!stoppingToken.IsCancellationRequested)
                    {
                        // Note only 1 client connection is supported, i.e., multiple clients cannot connect in parallel.
                        using(_tcpClient = await tcpListener.AcceptTcpClientAsync())
                        {
                            await _client.ExecuteAsync(_tcpClient, stoppingToken);
                        }
                        _tcpClient = null;
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

            _logger.LogInformation("Worker stopping at: {time}", DateTime.Now);
        }
    }
}
