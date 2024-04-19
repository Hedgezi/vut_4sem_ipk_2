using System.Net.Sockets;
using System.Text;
using vut_ipk2.Common.Structures;

namespace vut_ipk2.TcpServer.Facades;

public class TcpMessageReceiver
{
    private static readonly Encoding ConversionEncoding = Encoding.ASCII;
    private const int BufferSize = 4096;

    private readonly byte[] _buffer = new byte[BufferSize];
    private readonly FixedSizeQueue<string> _queuedMessages = new(50);
    private readonly StringBuilder _messageBuilder = new();

    /// <summary>
    /// Receive and return message from the client.
    /// It receive bytes from TCP stream, then split them into messages and return them one by one,
    /// while keeping the rest in queue.
    /// </summary>
    /// <param name="client">Client to receive message</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>Message string</returns>
    public async Task<string> ReceiveMessageAsync(TcpClient client, CancellationToken token)
    {
        if (_queuedMessages.Count > 0)
            return _queuedMessages.Dequeue();
        
        var receivedMessageBytes = await client.GetStream().ReadAsync(_buffer, token);
        
        // If the token is cancelled, we need to return from the method
        if (token.IsCancellationRequested)
            return string.Empty;

        // If the last message is not fully built, we need to keep it in the builder
        var lastMessageToBuild = receivedMessageBytes == BufferSize && !_buffer[^1].Equals(0x0A) && !_buffer[^2].Equals(0x0D);

        var receivedMessages = ConversionEncoding.GetString(_buffer, 0, receivedMessageBytes).Split("\r\n");
        
        for (var i = 0; i < receivedMessages.Length; i++)
        {
            if (string.IsNullOrEmpty(receivedMessages[i]))
                break;
            
            // If the last message is ended with \r\n, we need to keep it in the buffer
            if (i == receivedMessages.Length - 1 && !lastMessageToBuild)
            {
                _queuedMessages.Enqueue(receivedMessages[i]);
                break;
            } 
            
            // If the last message is not fully built, we need to keep it in the builder and wait for the next message
            if (i == receivedMessages.Length - 1 && lastMessageToBuild)
            {
                _messageBuilder.Append(receivedMessages[i]);
                continue;
            }

            if (_messageBuilder.Length > 0)
            {
                _messageBuilder.Append(receivedMessages[i]);
                _queuedMessages.Enqueue(_messageBuilder.ToString());
                _messageBuilder.Clear();
            }
            else
            {
                _queuedMessages.Enqueue(receivedMessages[i]);
            }
        }
        
        return _queuedMessages.Dequeue();
    }
}