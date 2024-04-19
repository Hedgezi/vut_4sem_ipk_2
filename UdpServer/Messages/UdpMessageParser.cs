using System.Buffers.Binary;
using System.Text;

namespace vut_ipk2.UdpServer.Messages;

public static class UdpMessageParser
{
    private static readonly Encoding TextEncoding = Encoding.ASCII;

    public static (ushort messageId, string username, string displayName, string secret) ParseAuthMessage(
        byte[] message)
    {
        var messageId = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan()[1..3]);
        var username = ConvertAsciiBytesToString(message, 3);
        var displayName = ConvertAsciiBytesToString(message, 3 + username.Length);
        var secret = ConvertAsciiBytesToString(message, 3 + username.Length + displayName.Length);
        
        if (!ValidateUsername(username) ||
            !ValidateDisplayName(displayName) ||
            !ValidateSecret(secret))
            throw new ArgumentException("Invalid message contents.");

        return (messageId, username, displayName, secret);
    }

    public static (ushort messageId, string channelName, string displayName) ParseJoinMessage(byte[] message)
    {
        var messageId = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan()[1..3]);
        var channelName = ConvertAsciiBytesToString(message, 3);
        var displayName = ConvertAsciiBytesToString(message, 3 + channelName.Length);
        
        if (!ValidateChannelId(channelName) ||
            !ValidateDisplayName(displayName))
            throw new ArgumentException("Invalid message contents.");

        return (messageId, channelName, displayName);
    }

    public static (ushort messageId, string displayName, string messageContents) ParseMsgMessage(byte[] message)
    {
        var messageId = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan()[1..3]);
        var displayName = ConvertAsciiBytesToString(message, 3);
        var messageContents = ConvertAsciiBytesToString(message, 3 + displayName.Length);

        if (!ValidateDisplayName(displayName) ||
            !ValidateMessageContents(messageContents))
            throw new ArgumentException("Invalid message contents.");

        return (messageId, displayName, messageContents);
    }

    public static (ushort messageId, string displayName, string messageContents) ParseErrMessage(byte[] message)
    {
        var messageId = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan()[1..3]);
        var displayName = ConvertAsciiBytesToString(message, 3);
        var messageContents = ConvertAsciiBytesToString(message, 3 + displayName.Length);
        
        if (!ValidateDisplayName(displayName) ||
            !ValidateMessageContents(messageContents))
            throw new ArgumentException("Invalid message contents.");

        return (messageId, displayName, messageContents);
    }
    
    /* Helper methods */

    private static string ConvertAsciiBytesToString(byte[] bytes, int startIndex)
    {
        var shortByteArray = bytes[startIndex] == 0x00 ? bytes[++startIndex..] : bytes[startIndex..];
        var length = Array.IndexOf(shortByteArray, (byte)0x00);

        return TextEncoding.GetString(shortByteArray, 0, length).Trim('\0');
    }
    
    /* Validation methods */
    
    private static bool ValidateUsername(string username) =>
        ValidateStringAlphanumWithDash(username) && username.Length <= 20;
    
    private static bool ValidateChannelId(string channelId) =>
        ValidateStringAlphanumWithDash(channelId) && channelId.Length <= 20;
    
    private static bool ValidateSecret(string secret) =>
        ValidateStringAlphanumWithDash(secret) && secret.Length <= 128;
    
    private static bool ValidateDisplayName(string displayName) =>
        ValidateStringWithAsciiRange(displayName, 0x21, 0x7E) && displayName.Length <= 20;
    
    private static bool ValidateMessageContents(string messageContents) =>
        ValidateStringWithAsciiRange(messageContents, 0x20, 0x7E) && messageContents.Length <= 1400;

    private static bool ValidateStringWithAsciiRange(string input, int min, int max) =>
        input.ToCharArray().All(character => character >= min && character <= max);

    private static bool ValidateStringAlphanumWithDash(string input) =>
        input.ToCharArray().All(character =>
            character == 0x2D || (character >= 0x30 && character <= 0x39) || (character >= 0x41 && character <= 0x5A) ||
            (character >= 0x61 && character <= 0x7A));
}