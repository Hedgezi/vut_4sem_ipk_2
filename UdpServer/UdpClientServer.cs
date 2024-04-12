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
    private readonly AuthDataChecker _authDataChecker;

    private ushort _messageCounter;
    private FsmState _fsmState = FsmState.Auth;

    private readonly FixedSizeQueue<ushort> _awaitedMessages = new(200); // all CONFIRM messages go here
    private readonly FixedSizeQueue<ushort> _receivedMessages = new(200); // remember received messages for deduplication

    private readonly UdpClient _client;
    private Room _currentRoom;

    public UdpClientServer(IPAddress ip, int confirmationTimeout, int maxRetransmissions,
        AuthDataChecker authDataChecker, IPEndPoint remoteEndPoint)
    {
        _confirmationTimeout = confirmationTimeout;
        _maxRetransmissions = maxRetransmissions;
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

            switch ((MessageType)message[0])
            {
                case MessageType.CONFIRM:
                    _awaitedMessages.Enqueue(BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan()[1..3]));

                    break;
                case MessageType.AUTH:
                    var (authMessageId, authUsername, authDisplayName, authSecret) = UdpMessageParser.ParseAuthMessage(message);

                    Task.Run(() => AuthRecurrent(authMessageId, authUsername, authDisplayName, authSecret));
                    break;
                case MessageType.JOIN:
                    var (joinMessageId, joinRoomName, joinDisplayName) = UdpMessageParser.ParseJoinMessage(message);
                    
                    Task.Run(() => Join(joinMessageId, joinDisplayName, joinRoomName));
                    break;
                case MessageType.MSG:
                    var (msgMessageId, msgDisplayName, msgMessage) = UdpMessageParser.ParseMsgMessage(message);
                    
                    Task.Run(() => Msg(msgMessageId, msgDisplayName, msgMessage));
                    break;
            }
        }
    }
    
    public async Task OnNextAsync(MessageInfo value)
    {
        var message = UdpMessageGenerator.GenerateMsgMessage(_messageCounter, value.From, value.Message);
        
        await SendAndAwaitConfirmResponse(message, _messageCounter++);
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