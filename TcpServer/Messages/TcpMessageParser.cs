using System.Text.RegularExpressions;
using vut_ipk2.Common.Enums;

namespace vut_ipk2.TcpServer.Messages;

public static class TcpMessageParser
{
    public static (string username, string displayName, string secret) ParseAuthMessage(string message)
    {
        var match = Regex.IsMatch(message, @"^AUTH [A-Za-z0-9\-]{1,20} AS [\x21-\x7E]{1,20} USING [A-Za-z0-9\-]{1,128}$");
        if (!match)
            throw new ArgumentException("Invalid message format");
        
        var messageParts = message.Trim().Split(' ');

        return (messageParts[1], messageParts[3], messageParts[5].TrimEnd());
    }
    
    public static (string channelName, string displayName) ParseJoinMessage(string message)
    {
        var match = Regex.IsMatch(message, @"^JOIN [A-Za-z0-9\-]{1,20} AS [\x21-\x7E]{1,20}$");
        if (!match)
            throw new ArgumentException("Invalid message format");
        
        var messageParts = message.Trim().Split(' ');
        
        return (messageParts[1], messageParts[3].TrimEnd());
    }

    public static (string displayName, string messageContents) ParseMsgMessage(string message)
    {
        var match = Regex.IsMatch(message, @"^MSG FROM [\x21-\x7E]{1,20} IS [\x20-\x7E]{1,1400}$");
        if (!match)
            throw new ArgumentException("Invalid message format");
        
        var messageParts = message.Trim().Split(' ', 5);
        
        var displayName = messageParts[2];
        var messageContents = messageParts[4];

        return (displayName, messageContents.TrimEnd());
    }
    
    public static (string displayName, string messageContents) ParseErrMessage(string message)
    {
        var match = Regex.IsMatch(message, @"^ERR FROM [\x21-\x7E]{1,20} IS [\x20-\x7E]{1,1400}$");
        if (!match)
            throw new ArgumentException("Invalid message format");
        
        var messageParts = message.Trim().Split(' ', 5);
        
        var displayName = messageParts[2];
        var messageContents = messageParts[4];

        return (displayName, messageContents.TrimEnd());
    }
    
    public static MessageType ParseMessageType(string message) =>
        Enum.Parse<MessageType>(message.Trim().Split(' ', 2)[0]);
}