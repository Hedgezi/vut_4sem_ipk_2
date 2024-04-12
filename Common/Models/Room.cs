using vut_ipk2.Common.Interfaces;
using vut_ipk2.Common.Managers;

namespace vut_ipk2.Common.Models;

public class Room : IAsyncObservable<MessageInfo>
{
    public string Name { get; }
    
    private readonly HashSet<IAsyncObserver<MessageInfo>> _observers = new();
    
    public Room(string name)
    {
        Name = name;
    }
    
    public async Task SubscribeAsync(IAsyncObserver<MessageInfo> observer)
    {
        _observers.Add(observer);
    }
    
    public async Task UnsubscribeAsync(IAsyncObserver<MessageInfo> observer)
    {
        _observers.Remove(observer);

        if (_observers.Count == 0)
            RoomManager.RemoveRoom(this);
    }
    
    public async Task NotifyAsync(IAsyncObserver<MessageInfo> observerWhoSend, MessageInfo value)
    {
        foreach (var observer in _observers)
        {
            if (observer == observerWhoSend && value.From != "Server")
                continue;
            
            Task.Run(() => observer.OnNextAsync(value));
        }
    }
}