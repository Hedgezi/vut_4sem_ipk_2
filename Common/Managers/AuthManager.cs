namespace vut_ipk2.Common.Managers;

/// <summary>
/// Manager for authentication. Does not provide any checks, only stores the usernames in a hash set.
/// Allows only single connection per unique user account (username) at the most.
/// </summary>
public static class AuthManager
{
    private static readonly HashSet<string> Users = new();
    
    public static bool CheckAuthData(string username, string secret) =>
        Users.Add(username);
    
    public static void UnLogin(string username) =>
        Users.Remove(username);
}