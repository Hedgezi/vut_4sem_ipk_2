using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using vut_ipk2.Common.Auth;
using vut_ipk2.Common.Enums;
using vut_ipk2.Common.Structures;
using vut_ipk2.UdpServer.Messages;

namespace vut_ipk2.UdpServer;

public class UdpClientServer
{
    private readonly int _confirmationTimeout;
    private readonly int _maxRetransmissions;
    private readonly AuthDataChecker _authDataChecker;

    private ushort _messageCounter;
    private FsmState _fsmState;

    private readonly FixedSizeQueue<ushort> _awaitedMessages = new(200); // all CONFIRM messages go here

    // remember received messages for deduplication
    private readonly FixedSizeQueue<ushort> _receivedMessages = new(200);

    private readonly UdpClient _client;

    public UdpClientServer(IPAddress ip, int confirmationTimeout, int maxRetransmissions,
        AuthDataChecker authDataChecker)
    {
        _confirmationTimeout = confirmationTimeout;
        _maxRetransmissions = maxRetransmissions;
        _authDataChecker = authDataChecker;

        _client = new UdpClient(new IPEndPoint(ip, 0)); // TODO: Set port
    }
    
    public static async Task<UdpClientServer?> CreateNewConnectionAfterAuth(IPAddress ip, int confirmationTimeout, int maxRetransmissions,
        AuthDataChecker authDataChecker, ushort messageId, string username, string displayName, string secret)
    {
        var client = new UdpClientServer(ip, confirmationTimeout, maxRetransmissions, authDataChecker);

        client._awaitedMessages.Enqueue(messageId);
        
        if (!client._authDataChecker.CheckAuthData(username, secret))
        {
            for (var i = 0; i < 1 + client._maxRetransmissions; i++)
            {
                await client._client.SendAsync(
                    UdpMessageGenerator.GenerateReplyMessage(client._messageCounter, false, messageId, MessageContents.AuthFailed),
                    client._messageCounter++
                );

                await Task.Delay(client._confirmationTimeout);

                if (client._awaitedMessages.Contains(messageId))
                    return null;
            }
            
            return client;
        }
        
        return client;
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

                    Task.Run(() => Auth(authMessageId, authUsername, authDisplayName, authSecret));
                    break;
            }
        }
    }

    private async Task AuthRecurrent(ushort messageId, string username, string displayName, string secret)
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
        await JoinARoom();
        _fsmState = FsmState.Open;

    }

    private async Task Auth(ushort messageId, string username, string displayName, string secret)
    {
        if (_fsmState is not FsmState.Auth)
        {
            // TODO: Send error msg 
            return;
        }

        await SendConfirmMessage(messageId);

        if (!IsItNewMessage(messageId))
            return;
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
        await JoinARoom();
        _fsmState = FsmState.Open;
    }

    private async Task JoinARoom()
    {
        
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