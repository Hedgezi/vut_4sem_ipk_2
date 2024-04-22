using vut_ipk2.Common.Interfaces;
using vut_ipk2.Common.Managers;

namespace vut_ipk2.Common.Models;

/// <summary>
/// Object representing a chat room.
/// Implements IAsyncObservable to allow for subscribing and unsubscribing to messages.
/// </summary>
public class Room : IAsyncObservable<MessageInfo>
{
    public string Name { get; }
    
    private readonly HashSet<IAsyncObserver<MessageInfo>> _observers = new();
    
    public Room(string name)
    {
        Name = name;
    }
    
    public async Task SubscribeAsync(IAsyncObserver<MessageInfo> observer) =>
        _observers.Add(observer);
    
    public async Task UnsubscribeAsync(IAsyncObserver<MessageInfo> observer)
    {
        _observers.Remove(observer);

        if (_observers.Count == 0)
            RoomManager.RemoveRoom(this);
    }
    
    /// <summary>
    /// Notify all observers about the new message.
    /// Don't notify the observer who sent the message, if message is server's.
    /// </summary>
    /// <param name="observerWhoSend">Observer who sent the message</param>
    /// <param name="value">Message</param>
    public async Task NotifyAsync(IAsyncObserver<MessageInfo> observerWhoSend, MessageInfo value)
    {
        foreach (var observer in _observers)
        {
            if (observer == observerWhoSend && value.From != "Server")
                continue;
            
            await observer.OnNextAsync(value);
        }
    }
}