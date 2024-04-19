using vut_ipk2.Common.Models;

namespace vut_ipk2.Common.Managers;

public static class RoomManager
{
    private static readonly HashSet<Room> Rooms = new();
    
    public static Room GetRoom(string roomName)
    {
        var room = Rooms.FirstOrDefault(r => r.Name == roomName);

        if (room == null)
        {
            room = new Room(roomName);
            Rooms.Add(room);
        }
        
        return room;
    }
    
    public static void RemoveRoom(Room room)
    {
        Rooms.Remove(room);
    }
}