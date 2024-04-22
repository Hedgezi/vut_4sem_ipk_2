using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using vut_ipk2.Common.Enums;
using vut_ipk2.Common.Interfaces;
using vut_ipk2.Common.Managers;
using vut_ipk2.Common.Models;
using vut_ipk2.Common.Structures;
using vut_ipk2.UdpServer.Messages;

namespace vut_ipk2.UdpServer;

public class UdpClientServer : IAsyncObserver<MessageInfo>
{
    private readonly int _confirmationTimeout;
    private readonly int _maxRetransmissions;
    private readonly IPEndPoint _remoteEndPoint;
    private readonly UdpMainServer _mainServer;

    private ushort _messageCounter;
    private FsmState _fsmState = FsmState.Auth;
    private Room _currentRoom;
    private string _lastUsedDisplayName;
    private string _username;
    private readonly CancellationTokenSource _cancellationTokenSource = new(); // if server is shutting down, we need to cancel the client

    private readonly FixedSizeQueue<ushort> _awaitedMessages = new(200); // all CONFIRM messages go here
    private readonly FixedSizeQueue<ushort>
        _receivedMessages = new(200); // remember received messages for deduplication

    private readonly UdpClient _client;

    public UdpClientServer(IPAddress ip, int confirmationTimeout, int maxRetransmissions, UdpMainServer mainServer,
        IPEndPoint remoteEndPoint)
    {
        _confirmationTimeout = confirmationTimeout;
        _maxRetransmissions = maxRetransmissions;
        _remoteEndPoint = remoteEndPoint;
        _mainServer = mainServer;

        _client = new UdpClient(new IPEndPoint(ip, 0));
        _client.Connect(remoteEndPoint);
    }

    public async Task MainLoopAsync()
    {
        while (true)
        {
            var result = await _client.ReceiveAsync(_cancellationTokenSource.Token);
            var message = result.Buffer;

            if (_cancellationTokenSource.Token.IsCancellationRequested)
                return;

            try
            {
                await Console.Out.WriteLineAsync(
                    $"RECV {_remoteEndPoint.Address}:{_remoteEndPoint.Port} | {((MessageType)message[0]).ToString()}"
                );

                switch ((MessageType)message[0])
                {
                    case MessageType.CONFIRM:
                        _awaitedMessages.Enqueue(BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan()[1..3]));

                        break;
                    case MessageType.AUTH:
                        var (authMessageId, authUsername, authDisplayName, authSecret) =
                            UdpMessageParser.ParseAuthMessage(message);

                        Task.Run(() => AuthRecurrent(authMessageId, authUsername, authDisplayName, authSecret));
                        break;
                    case MessageType.JOIN:
                        var (joinMessageId, joinRoomName, joinDisplayName) =
                            UdpMessageParser.ParseJoinMessage(message);

                        Task.Run(() => Join(joinMessageId, joinDisplayName, joinRoomName));
                        break;
                    case MessageType.MSG:
                        var (msgMessageId, msgDisplayName, msgMessage) = UdpMessageParser.ParseMsgMessage(message);

                        Task.Run(() => Msg(msgMessageId, msgDisplayName, msgMessage));
                        break;
                    case MessageType.ERR:
                        var (errMessageId, errDisplayName, errMessageContents) =
                            UdpMessageParser.ParseErrMessage(message);

                        await Err(errMessageId, errDisplayName, errMessageContents);
                        return;
                    case MessageType.BYE:
                        var byeMessageId = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan()[1..3]);

                        await Bye(byeMessageId);
                        return;
                    default:
                        await ClientError(BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan()[1..3]));
                        return;
                }
            }
            catch (Exception)
            {
                var messageId = message.Length > 2
                    ? BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan()[1..3])
                    : (ushort)0;

                await ClientError(messageId);
                return;
            }
        }
    }

    public async Task OnNextAsync(MessageInfo value)
    {
        await SendAndAwaitConfirmResponse(
            UdpMessageGenerator.GenerateMsgMessage(_messageCounter, value.From, value.Message),
            _messageCounter++
        );
    }
    
    public async Task EndSession()
    {
        await SendAndAwaitConfirmResponse(
            UdpMessageGenerator.GenerateByeMessage(_messageCounter),
            _messageCounter++,
            true
        );
        
        await EndClientServerConnection();
    }

    /* AUTH */

    public async Task Auth(ushort messageId, string username, string displayName, string secret)
    {
        _receivedMessages.Enqueue(messageId);

        if (!AuthManager.CheckAuthData(username, secret))
        {
            await SendAndAwaitConfirmResponse(
                UdpMessageGenerator.GenerateReplyMessage(_messageCounter, false, messageId, MessageContents.AuthFailed),
                _messageCounter++
            );
            return;
        }

        await SendAndAwaitConfirmResponse(
            UdpMessageGenerator.GenerateReplyMessage(_messageCounter, true, messageId, MessageContents.AuthSuccess),
            _messageCounter++
        );
        await JoinARoom(displayName);
        _lastUsedDisplayName = displayName;
        _username = username;
        _fsmState = FsmState.Open;
    }

    private async Task AuthRecurrent(ushort messageId, string username, string displayName, string secret)
    {
        if (_fsmState is not FsmState.Auth)
        {
            await ClientError(messageId);
            return;
        }

        await SendConfirmMessage(messageId);

        if (!IsItNewMessage(messageId))
            return;

        await Auth(messageId, username, displayName, secret);
    }

    /* JOIN */

    private async Task Join(ushort messageId, string displayName, string roomName)
    {
        if (_fsmState is not FsmState.Open)
        {
            await ClientError(messageId);
            return;
        }

        await SendConfirmMessage(messageId);

        if (!IsItNewMessage(messageId))
            return;
        _receivedMessages.Enqueue(messageId);

        await _currentRoom.UnsubscribeAsync(this);
        await _currentRoom.NotifyAsync(this, new MessageInfo(
            "Server",
            MessageContents.GenerateLeftRoomMessage(displayName, _currentRoom.Name)
        ));

        await JoinARoom(displayName, roomName);

        await SendAndAwaitConfirmResponse(
            UdpMessageGenerator.GenerateReplyMessage(_messageCounter, true, messageId, MessageContents.JoinSuccess),
            _messageCounter++
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

    private async Task Msg(ushort messageId, string displayName, string message)
    {
        if (_fsmState is not FsmState.Open)
        {
            await ClientError(messageId);
            return;
        }

        await SendConfirmMessage(messageId);

        if (!IsItNewMessage(messageId))
            return;
        _receivedMessages.Enqueue(messageId);

        await _currentRoom.NotifyAsync(this, new MessageInfo(displayName, message));

        _lastUsedDisplayName = displayName;
    }

    /* CLIENT SHUTDOWN */

    private async Task Err(ushort messageId, string displayName, string message)
    {
        await SendConfirmMessage(messageId);

        if (!IsItNewMessage(messageId))
            return;
        _receivedMessages.Enqueue(messageId);

        await SendAndAwaitConfirmResponse(
            UdpMessageGenerator.GenerateByeMessage(_messageCounter),
            _messageCounter++
        );

        _lastUsedDisplayName = displayName;

        await EndClientServerConnection();
    }

    private async Task Bye(ushort messageId)
    {
        await SendConfirmMessage(messageId);

        if (!IsItNewMessage(messageId))
            return;
        _receivedMessages.Enqueue(messageId);

        await EndClientServerConnection();
    }

    private async Task ClientError(ushort messageId)
    {
        await SendConfirmMessage(messageId);

        if (!IsItNewMessage(messageId))
            return;
        _receivedMessages.Enqueue(messageId);

        await SendClientError();

        await EndClientServerConnection();
    }

    private async Task SendClientError()
    {
        await SendAndAwaitConfirmResponse(
            UdpMessageGenerator.GenerateErrMessage(_messageCounter, "Server", MessageContents.ClientError),
            _messageCounter++,
            true
        );

        await SendAndAwaitConfirmResponse(
            UdpMessageGenerator.GenerateByeMessage(_messageCounter),
            _messageCounter++,
            true
        );
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

    /* HELPERS */

    private async Task SendAndAwaitConfirmResponse(byte[] message, ushort messageId, bool isClientError = false)
    {
        for (var i = 0; i < 1 + _maxRetransmissions; i++)
        {
            try
            {
                await _client.SendAsync(message, message.Length);
                await Console.Out.WriteLineAsync(
                    $"SENT {_remoteEndPoint.Address}:{_remoteEndPoint.Port} | {((MessageType)message[0]).ToString()}"
                );
            }
            catch (SocketException)
            {
                await Console.Error.WriteLineAsync("UDP: Failed to send the message.");
                break;
            }

            await Task.Delay(_confirmationTimeout);

            if (_awaitedMessages.Contains(messageId))
                return;
        }
        
        await Console.Error.WriteLineAsync("UDP: Client did not respond to the message.");

        if (!isClientError)
            await SendClientError();

        await EndClientServerConnection();
    }

    private async Task SendConfirmMessage(ushort messageId)
    {
        var confirmMessage = UdpMessageGenerator.GenerateConfirmMessage(messageId);

        await _client.SendAsync(confirmMessage, confirmMessage.Length);
        await Console.Out.WriteLineAsync($"SENT {_remoteEndPoint.Address}:{_remoteEndPoint.Port} | CONFIRM");
    }

    private bool IsItNewMessage(ushort messageId)
    {
        return !_receivedMessages.Contains(messageId);
    }
}