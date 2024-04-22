namespace vut_ipk2.Common.Interfaces;

/// <summary>
/// Interface for server which establishes connection with clients.
/// </summary>
public interface IMainServer
{
    /// <summary>
    /// Establish connection with clients in an infinite loop.
    /// </summary>
    /// <returns>Task</returns>
    public Task AcceptNewUserLoopAsync();

    /// <summary>
    /// Power off the server by sending a message to all clients and releasing all resources.
    /// </summary>
    /// <returns>Task</returns>
    public Task PowerOffAsync();
}