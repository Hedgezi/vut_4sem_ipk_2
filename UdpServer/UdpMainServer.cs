using System.Net;
using System.Net.Sockets;
using vut_ipk2.Common.Enums;
using vut_ipk2.UdpServer.Messages;

namespace vut_ipk2.UdpServer;

public class UdpMainServer
{
    private readonly IPAddress _ip;
    private readonly int _port;
    private readonly int _confirmationTimeout;
    private readonly int _maxRetransmissions;

    private readonly List<UdpClientServer> _clients = new();

    private readonly UdpClient _client;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public UdpMainServer(IPAddress ip, int port, int confirmationTimeout, int maxRetransmissions)
    {
        _ip = ip;
        _port = port;
        _confirmationTimeout = confirmationTimeout;
        _maxRetransmissions = maxRetransmissions;

        _client = new UdpClient(new IPEndPoint(_ip, _port));
    }

    public async Task AcceptNewUserLoopAsync()
    {
        while (true)
        {
            try
            {
                var result = await _client.ReceiveAsync(_cancellationTokenSource.Token);
                
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    return;

                if (result.Buffer[0] != (byte)MessageType.AUTH)
                    continue;

                var (messageId, username, displayName, secret) = UdpMessageParser.ParseAuthMessage(result.Buffer);
                await Console.Out.WriteLineAsync(
                    $"RECV {result.RemoteEndPoint.Address}:{result.RemoteEndPoint.Port} | AUTH"
                );

                await _client.SendAsync(UdpMessageGenerator.GenerateConfirmMessage(messageId), result.RemoteEndPoint);
                await Console.Out.WriteLineAsync(
                    $"SENT {result.RemoteEndPoint.Address}:{result.RemoteEndPoint.Port} | CONFIRM"
                );

                var newClient = new UdpClientServer(
                    _ip,
                    _confirmationTimeout,
                    _maxRetransmissions,
                    this,
                    result.RemoteEndPoint
                );

                _clients.Add(newClient);
                Task.Run(() => newClient.MainLoopAsync());
                Task.Run(() => newClient.Auth(messageId, username, displayName, secret));
            }
            catch (Exception)
            {
                continue;
            }
        }
    }

    public void RemoveClient(UdpClientServer client)
    {
        _clients.Remove(client);
    }
}