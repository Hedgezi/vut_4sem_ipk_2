namespace vut_ipk2.Common.Interfaces;

public interface IAsyncObserver<T>
{
    public Task OnNextAsync(T value);
}