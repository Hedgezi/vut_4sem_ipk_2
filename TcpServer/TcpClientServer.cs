using System.Net;
using System.Net.Sockets;
using vut_ipk2.Common.Enums;
using vut_ipk2.Common.Interfaces;
using vut_ipk2.Common.Managers;
using vut_ipk2.Common.Models;
using vut_ipk2.TcpServer.Facades;
using vut_ipk2.TcpServer.Messages;

namespace vut_ipk2.TcpServer;

public class TcpClientServer : IAsyncObserver<MessageInfo>
{
    private readonly TcpMessageReceiver _messageReceiver;
    private readonly TcpMainServer _mainServer;
    private readonly IPEndPoint _remoteEndPoint;
    
    private FsmState _fsmState = FsmState.Auth;
    private Room _currentRoom;
    private string _lastUsedDisplayName;
    private string _username;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    private readonly TcpClient _client;
    
    public TcpClientServer(TcpClient client, TcpMessageReceiver messageReceiver, TcpMainServer mainServer, IPEndPoint remoteEndPoint)
    {
        _client = client;
        _messageReceiver = messageReceiver;
        _remoteEndPoint = remoteEndPoint;
        _mainServer = mainServer;
    }
    
    public async Task MainLoopAsync()
    {
        while (true)
        {
            try
            {
                var message = await _messageReceiver.ReceiveMessageAsync(_client, _cancellationTokenSource.Token);
                
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    return;
                
                await Console.Out.WriteLineAsync(
                    $"RECV {_remoteEndPoint.Address}:{_remoteEndPoint.Port} | {TcpMessageParser.ParseMessageType(message)}"
                );
                
                switch (TcpMessageParser.ParseMessageType(message))
                {
                    case MessageType.AUTH:
                        var (authUsername, authDisplayName, authSecret) =
                            TcpMessageParser.ParseAuthMessage(message);

                        Task.Run(() => AuthRecurrent(authUsername, authDisplayName, authSecret));
                        break;
                    case MessageType.JOIN:
                        var (joinRoomName, joinDisplayName) =
                            TcpMessageParser.ParseJoinMessage(message);

                        Task.Run(() => Join(joinDisplayName, joinRoomName));
                        break;
                    case MessageType.MSG:
                        var (msgDisplayName, msgMessage) = TcpMessageParser.ParseMsgMessage(message);

                        Task.Run(() => Msg(msgDisplayName, msgMessage));
                        break;
                    case MessageType.ERR:
                        var (errDisplayName, _) =
                            TcpMessageParser.ParseErrMessage(message);

                        await Err(errDisplayName);
                        return;
                    case MessageType.BYE:
                        await Bye();
                        return;
                    default:
                        await ClientError();
                        return;
                }
            }
            catch (Exception)
            {
                await ClientError();
                return;
            }
        }
    }
    
    public async Task OnNextAsync(MessageInfo value)
    {
        await _client.Client.SendAsync(
            TcpMessageGenerator.GenerateMsgMessage(value.From, value.Message)
        );
    }
    
    /* AUTH */

    public async Task Auth(string username, string displayName, string secret)
    {
        if (!AuthManager.CheckAuthData(username, secret))
        {
            await _client.Client.SendAsync(
                TcpMessageGenerator.GenerateReplyMessage(false, MessageContents.AuthFailed)
            );
            return;
        }

        await _client.Client.SendAsync(
            TcpMessageGenerator.GenerateReplyMessage(true, MessageContents.AuthSuccess)
        );
        await JoinARoom(displayName);
        _lastUsedDisplayName = displayName;
        _username = username;
        _fsmState = FsmState.Open;
    }

    private async Task AuthRecurrent(string username, string displayName, string secret)
    {
        if (_fsmState is not FsmState.Auth)
        {
            await ClientError();
            return;
        }

        await Auth(username, displayName, secret);
    }
    
    /* JOIN */

    private async Task Join(string displayName, string roomName)
    {
        if (_fsmState is not FsmState.Open)
        {
            await ClientError();
            return;
        }

        await _currentRoom.UnsubscribeAsync(this);
        await _currentRoom.NotifyAsync(this, new MessageInfo(
            "Server",
            MessageContents.GenerateLeftRoomMessage(displayName, _currentRoom.Name)
        ));

        await JoinARoom(displayName, roomName);

        await _client.Client.SendAsync(
            TcpMessageGenerator.GenerateReplyMessage(true, MessageContents.JoinSuccess)
        );

        _lastUsedDisplayName = displayName;
    }
    
    private async Task JoinARoom(string displayName, string roomName = "default_room")
    {
        var room = RoomManager.GetRoom(roomName);

        await room.SubscribeAsync(this);
        _currentRoom = room;

        await room.NotifyAsync(this, new MessageInfo(
            "Server",
            MessageContents.GenerateJoinRoomMessage(displayName, roomName)
        ));
    }
    
    /* MSG */

    private async Task Msg(string displayName, string message)
    {
        if (_fsmState is not FsmState.Open)
        {
            await ClientError();
            return;
        }

        await _currentRoom.NotifyAsync(this, new MessageInfo(displayName, message));

        _lastUsedDisplayName = displayName;
    }

    /* CLIENT SHUTDOWN */

    private async Task Err(string displayName)
    {
        await _client.Client.SendAsync(TcpMessageGenerator.GenerateByeMessage());

        _lastUsedDisplayName = displayName;

        await EndClientServerConnection();
    }

    private async Task Bye()
    {
        await EndClientServerConnection();
    }

    private async Task ClientError()
    {
        await SendClientError();

        await EndClientServerConnection();
    }

    private async Task SendClientError()
    {
        await _client.Client.SendAsync(TcpMessageGenerator.GenerateErrMessage("Server", MessageContents.ClientError));

        await _client.Client.SendAsync(TcpMessageGenerator.GenerateByeMessage());
    }

    private async Task EndClientServerConnection()
    {
        if (!_client.Client.Connected)
            return;
        
        await _currentRoom.UnsubscribeAsync(this);
        await _currentRoom.NotifyAsync(this, new MessageInfo(
            "Server",
            MessageContents.GenerateLeftRoomMessage(_lastUsedDisplayName, _currentRoom.Name)
        ));

        _client.Close();
        _client.Dispose();

        AuthManager.UnLogin(_username);
        _mainServer.RemoveClient(this);
        _fsmState = FsmState.End;

        await _cancellationTokenSource.CancelAsync();
    }
}