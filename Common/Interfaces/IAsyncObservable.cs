namespace vut_ipk2.Common.Interfaces;

/// <summary>
/// Interface for asynchronous observable objects.
/// </summary>
/// <typeparam name="T">
/// Type of the object that is used to communicate between the observable and the observer.
/// </typeparam>
public interface IAsyncObservable<T>
{
    public Task SubscribeAsync(IAsyncObserver<T> observer);
}