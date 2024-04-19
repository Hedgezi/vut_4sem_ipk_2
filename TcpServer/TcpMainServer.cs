using System.Net;
using System.Net.Sockets;
using vut_ipk2.Common.Enums;
using vut_ipk2.TcpServer.Facades;
using vut_ipk2.TcpServer.Messages;

namespace vut_ipk2.TcpServer;

public class TcpMainServer
{
    private readonly List<TcpClientServer> _clients = new();

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public TcpMainServer(IPAddress ip, int port)
    {
        _listener = new TcpListener(new IPEndPoint(ip, port));
    }

    public async Task AcceptNewUserLoopAsync()
    {
        _listener.Start();

        while (true)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();

                var tcpMessageReceiver = new TcpMessageReceiver();
                var receivedMessage =
                    await tcpMessageReceiver.ReceiveMessageAsync(client, _cancellationTokenSource.Token);

                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    return;

                if (TcpMessageParser.ParseMessageType(receivedMessage) != MessageType.AUTH)
                    continue;


                var (username, displayName, secret) = TcpMessageParser.ParseAuthMessage(receivedMessage);
                
                var endPoint = (IPEndPoint)client.Client.RemoteEndPoint!;
                await Console.Out.WriteLineAsync(
                    $"RECV {endPoint.Address}:{endPoint.Port} | AUTH"
                );

                var newClient = new TcpClientServer(
                    client,
                    tcpMessageReceiver,
                    this,
                    endPoint
                );

                _clients.Add(newClient);
                Task.Run(() => newClient.MainLoopAsync());
                Task.Run(() => newClient.Auth(username, displayName, secret));
            }
            catch (Exception)
            {
            }
        }
    }

    public void RemoveClient(TcpClientServer client)
    {
        _clients.Remove(client);
    }
}