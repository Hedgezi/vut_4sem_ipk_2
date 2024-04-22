namespace vut_ipk2.Common.Interfaces;

/// <summary>
/// Interface for asynchronous observer objects.
/// </summary>
/// <typeparam name="T">
/// Type of the object that is used to communicate between the observable and the observer.
/// </typeparam>
public interface IAsyncObserver<T>
{
    public Task OnNextAsync(T value);
}