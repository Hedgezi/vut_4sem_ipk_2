namespace vut_ipk2.Common.Managers;

public static class MessageBuilder
{
    public static string GenerateJoinRoomMessage(string displayName, string channelName)
        => $"{displayName} has joined {channelName}";
    
    public static string GenerateLeftRoomMessage(string displayName, string channelName)
        => $"{displayName} has left {channelName}";
}