namespace vut_ipk2.Common.Enums;

public static class MessageContents
{
    public const string AuthSuccess = "AUTH_SUCCESS";
    public const string AuthFailed = "AUTH_FAILED";
    public const string JoinSuccess = "JOIN_SUCCESS";
    public const string JoinFailed = "JOIN_FAILED";
    public const string ClientError = "CLIENT_ERROR";
    
    public static string GenerateJoinRoomMessage(string displayName, string channelName)
        => $"{displayName} has joined {channelName}";
    
    public static string GenerateLeftRoomMessage(string displayName, string channelName)
        => $"{displayName} has left {channelName}";
}