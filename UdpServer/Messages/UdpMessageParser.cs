using System.Buffers.Binary;
using System.Text;

namespace vut_ipk2.UdpServer.Messages;

public static class UdpMessageParser
{
    private static readonly Encoding TextEncoding = Encoding.ASCII;

    public static (ushort messageId, string username, string displayName, string secret) ParseAuthMessage(byte[] message)
    {
        var messageId = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan()[1..3]);
        var username = ConvertAsciiBytesToString(message, 3);
        var displayName = ConvertAsciiBytesToString(message, 3 + username.Length);
        var secret = ConvertAsciiBytesToString(message, 3 + username.Length + displayName.Length);

        return (messageId, username, displayName, secret);
    }
    
    public static (ushort messageId, string channelName, string displayName) ParseJoinMessage(byte[] message)
    {
        var messageId = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan()[1..3]);
        var channelName = ConvertAsciiBytesToString(message, 3);
        var displayName = ConvertAsciiBytesToString(message, 3 + channelName.Length);
        
        return (messageId, channelName, displayName);
    }

    public static (ushort messageId, string displayName, string messageContents) ParseMsgMessage(byte[] message)
    {
        var messageId = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan()[1..3]);
        var displayName = ConvertAsciiBytesToString(message, 3);
        var messageContents = ConvertAsciiBytesToString(message, 3 + displayName.Length);

        return (messageId, displayName, messageContents);
    }
    
    public static (ushort messageId, string displayName, string messageContents) ParseErrMessage(byte[] message)
    {
        var messageId = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan()[1..3]);
        var displayName = ConvertAsciiBytesToString(message, 3);
        var messageContents = ConvertAsciiBytesToString(message, 3 + displayName.Length);

        return (messageId, displayName, messageContents);
    }

    private static string ConvertAsciiBytesToString(byte[] bytes, int startIndex)
    {
        var shortByteArray = bytes[startIndex] == 0x00 ? bytes[++startIndex..] : bytes[startIndex..];
        var length = Array.IndexOf(shortByteArray, (byte)0x00);
        length = length == -1 ? shortByteArray.Length - 1 : length + 1;

        return TextEncoding.GetString(shortByteArray, 0, length).Trim('\0');
    }
}