using System.Net.Sockets;

namespace PcVolumeControlService;

public interface IClient
{
    public Task ExecuteAsync(TcpClient tcpClient, CancellationToken stoppingToken);
}
