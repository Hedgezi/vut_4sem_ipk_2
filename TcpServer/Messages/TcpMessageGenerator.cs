using System.Text;

namespace vut_ipk2.TcpServer.Messages;

public static class TcpMessageGenerator
{
    private static readonly Encoding TextEncoding = Encoding.ASCII;
    
    public static byte[] GenerateReplyMessage(bool result, string contents)
    {
        var resultString = result ? "OK" : "NOK";
        
        return TextEncoding.GetBytes($"REPLY {resultString} IS {contents}\r\n");
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