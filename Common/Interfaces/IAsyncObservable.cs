namespace vut_ipk2.Common.Interfaces;

public interface IAsyncObservable<T>
{
    public Task SubscribeAsync(IAsyncObserver<T> observer);
}