using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using vut_ipk2.Common.Auth;
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
    private readonly UdpMainServer _mainServer;
    private readonly AuthDataChecker _authDataChecker;

    private ushort _messageCounter;
    private FsmState _fsmState = FsmState.Auth;
    private Room _currentRoom;
    private string _lastUsedDisplayName;

    private readonly FixedSizeQueue<ushort> _awaitedMessages = new(200); // all CONFIRM messages go here
    private readonly FixedSizeQueue<ushort>
        _receivedMessages = new(200); // remember received messages for deduplication

    private readonly UdpClient _client;

    public UdpClientServer(IPAddress ip, int confirmationTimeout, int maxRetransmissions, UdpMainServer mainServer,
        AuthDataChecker authDataChecker, IPEndPoint remoteEndPoint)
    {
        _confirmationTimeout = confirmationTimeout;
        _maxRetransmissions = maxRetransmissions;
        _mainServer = mainServer;
        _authDataChecker = authDataChecker;

        _client = new UdpClient(new IPEndPoint(ip, 0));
        _client.Connect(remoteEndPoint);
    }

    public async Task MainLoopAsync()
    {
        while (true)
        {
            var result = await _client.ReceiveAsync();
            var message = result.Buffer;

            try
            {
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
                            UdpMessageParser.ParseMsgMessage(message); // ERR and MSG have the same structure

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
            catch (Exception e)
            {
                if (message.Length > 2)
                    await SendConfirmMessage(BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan()[1..3]));

                await ClientError(BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan()[1..3]));
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

    public async Task Auth(ushort messageId, string username, string displayName, string secret)
    {
        _receivedMessages.Enqueue(messageId);

        if (!_authDataChecker.CheckAuthData(username, secret))
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
        _fsmState = FsmState.Open;
    }

    private async Task AuthRecurrent(ushort messageId, string username, string displayName, string secret)
    {
        await SendConfirmMessage(messageId);

        if (!IsItNewMessage(messageId))
            return;

        if (_fsmState is not FsmState.Auth)
        {
            // TODO: Send error msg 
            return;
        }

        await Auth(messageId, username, displayName, secret);
    }

    private async Task Join(ushort messageId, string displayName, string roomName)
    {
        await SendConfirmMessage(messageId);

        if (!IsItNewMessage(messageId))
            return;
        _receivedMessages.Enqueue(messageId);

        if (_fsmState is not FsmState.Open)
        {
            // TODO
            return;
        }

        await _currentRoom.UnsubscribeAsync(this);
        await _currentRoom.NotifyAsync(this, new MessageInfo(
            "Server",
            MessageBuilder.GenerateLeftRoomMessage(displayName, _currentRoom.Name)
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
            MessageBuilder.GenerateJoinRoomMessage(displayName, roomName)
        ));
    }

    private async Task Msg(ushort messageId, string displayName, string message)
    {
        await SendConfirmMessage(messageId);

        if (!IsItNewMessage(messageId))
            return;
        _receivedMessages.Enqueue(messageId);

        if (_fsmState is not FsmState.Open)
        {
            // TODO: Send error msg 
            return;
        }

        await _currentRoom.NotifyAsync(this, new MessageInfo(displayName, message));

        _lastUsedDisplayName = displayName;
    }

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

        await SendAndAwaitConfirmResponse(
            UdpMessageGenerator.GenerateErrMessage(_messageCounter, "Server", MessageContents.ClientError),
            _messageCounter++
        );

        await SendAndAwaitConfirmResponse(
            UdpMessageGenerator.GenerateByeMessage(_messageCounter),
            _messageCounter++
        );

        await EndClientServerConnection();
    }

    private async Task EndClientServerConnection()
    {
        await _currentRoom.UnsubscribeAsync(this);
        await _currentRoom.NotifyAsync(this, new MessageInfo(
            "Server",
            MessageBuilder.GenerateLeftRoomMessage(_lastUsedDisplayName, _currentRoom.Name)
        ));

        _client.Close();
        _client.Dispose();

        _mainServer.RemoveClient(this);
        _fsmState = FsmState.End;
    }

    private async Task SendAndAwaitConfirmResponse(byte[] message, ushort messageId)
    {
        for (var i = 0; i < 1 + _maxRetransmissions; i++)
        {
            await _client.SendAsync(message, message.Length);

            await Task.Delay(_confirmationTimeout);

            if (_awaitedMessages.Contains(messageId))
                return;
        }

        // TODO: Handle timeout
        // Environment.Exit(1);
    }

    private async Task SendConfirmMessage(ushort messageId)
    {
        var confirmMessage = UdpMessageGenerator.GenerateConfirmMessage(messageId);

        await _client.SendAsync(confirmMessage, confirmMessage.Length);
    }

    private bool IsItNewMessage(ushort messageId)
    {
        return !_receivedMessages.Contains(messageId);
    }
}