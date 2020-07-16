using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PcVolumeControlService
{
    public interface IClient
    {
        public Task ExecuteAsync(TcpClient tcpClient, CancellationToken stoppingToken);
    }
}
