namespace vut_ipk2.Common.Auth;

public static class AuthDataChecker
{
    private static readonly List<string> _users = new();
    
    public static bool CheckAuthData(string username, string secret)
    {
        if (_users.Contains(username))
            return false;
        
        _users.Add(username);
        
        return true;
    }
    
    public static void UnLogin(string username)
    {
        _users.Remove(username);
    }
}