using vut_ipk2.Common.Models;

namespace vut_ipk2.Common.Managers;

public static class RoomManager
{
    private static readonly HashSet<Room> _rooms = new();
    
    public static Room GetRoom(string roomName)
    {
        var room = _rooms.FirstOrDefault(r => r.Name == roomName);

        if (room == null)
        {
            room = new Room(roomName);
            _rooms.Add(room);
        }
        
        return room;
    }
    
    public static void RemoveRoom(Room room)
    {
        _rooms.Remove(room);
    }
}