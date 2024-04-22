using System.Net;
using System.Net.Sockets;
using vut_ipk2.Common.Enums;
using vut_ipk2.Common.Interfaces;
using vut_ipk2.TcpServer.Facades;
using vut_ipk2.TcpServer.Messages;

namespace vut_ipk2.TcpServer;

public class TcpMainServer : IMainServer
{
    private readonly HashSet<TcpClientServer> _clients = new();

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
                var client = await _listener.AcceptTcpClientAsync(_cancellationTokenSource.Token);

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
                Task.Run(() => newClient.MainLoopAsync(username, displayName, secret));
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
    
    public async Task PowerOffAsync()
    {
        await _cancellationTokenSource.CancelAsync();
        _listener.Stop();

        HashSet<TcpClientServer> clientsToEnd = new(_clients);

        foreach (var client in clientsToEnd)
            await client.EndSession();
    }
}