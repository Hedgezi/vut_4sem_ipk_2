namespace vut_ipk2.Common.Interfaces;

public interface IMainServer
{
    public Task AcceptNewUserLoopAsync();

    public Task PowerOffAsync();
}