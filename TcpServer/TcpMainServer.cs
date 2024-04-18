using System.Net;
using System.Net.Sockets;
using vut_ipk2.Common.Enums;
using vut_ipk2.TcpServer.Facades;
using vut_ipk2.TcpServer.Messages;

namespace vut_ipk2.TcpServer;

public class TcpMainServer
{
    private readonly IPAddress _ip;
    private readonly int _port;

    private readonly List<TcpClientServer> _clients = new();

    private readonly TcpListener _listener;

    public TcpMainServer(IPAddress ip, int port)
    {
        _ip = ip;
        _port = port;

        _listener = new TcpListener(new IPEndPoint(_ip, _port));
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
                var receivedMessage = await tcpMessageReceiver.ReceiveMessageAsync(client);

                if ((MessageType)Enum.Parse(typeof(MessageType), receivedMessage.Split(' ', 2)[0]) != MessageType.AUTH)
                {
                    continue;
                }

                try
                {
                    var (username, displayName, secret) = TcpMessageParser.ParseAuthMessage(receivedMessage);
                    await Console.Out.WriteLineAsync(
                        $"RECV {((IPEndPoint)client.Client.RemoteEndPoint).Address}:{((IPEndPoint)client.Client.RemoteEndPoint).Port} | AUTH");
                }
                catch (Exception)
                {
                    continue;
                }

                var newClient = new TcpClientServer(
                    client,
                    this,
                    (IPEndPoint)client.Client.RemoteEndPoint
                );

                _clients.Add(newClient);
                Task.Run(() => newClient.MainLoopAsync());
                Task.Run(() => newClient.Auth(messageId, username, displayName, secret));
            }
            catch (Exception)
            {
            }
        }
    }
}