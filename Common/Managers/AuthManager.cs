namespace vut_ipk2.Common.Managers;

public static class AuthManager
{
    private static readonly HashSet<string> Users = new();
    
    public static bool CheckAuthData(string username, string secret) =>
        Users.Add(username);
    
    public static void UnLogin(string username) =>
        Users.Remove(username);
}