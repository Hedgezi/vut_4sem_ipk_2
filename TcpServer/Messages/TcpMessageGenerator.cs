using System.Text;

namespace vut_ipk2.TcpServer.Messages;

public static class TcpMessageGenerator
{
    private static readonly Encoding TextEncoding = Encoding.ASCII;
    
    public static byte[] GenerateAuthMessage(string username, string displayName, string secret)
    {
        return TextEncoding.GetBytes($"AUTH {username} AS {displayName} USING {secret}\r\n");
    }
    
    public static byte[] GenerateJoinMessage(string channelId, string displayName)
    {
        return TextEncoding.GetBytes($"JOIN {channelId} AS {displayName}\r\n");
    }
    
    public static byte[] GenerateMsgMessage(string displayName, string contents)
    {
        return TextEncoding.GetBytes($"MSG FROM {displayName} IS {contents}\r\n");
    }
    
    public static byte[] GenerateErrMessage(string displayName, string contents)
    {
        return TextEncoding.GetBytes($"ERR FROM {displayName} IS {contents}\r\n");
    }
    
    public static byte[] GenerateByeMessage()
    {
        return TextEncoding.GetBytes("BYE\r\n");
    }
}