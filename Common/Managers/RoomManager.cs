using vut_ipk2.Common.Models;

namespace vut_ipk2.Common.Managers;

/// <summary>
/// Manager for rooms.
/// </summary>
public static class RoomManager
{
    private static readonly HashSet<Room> Rooms = new();
    
    /// <summary>
    /// Finds or creates (if it doesn't exist) a room with the given name.
    /// </summary>
    /// <param name="roomName">Name of the room</param>
    /// <returns>Room</returns>
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
    
    public static void RemoveRoom(Room room) =>
        Rooms.Remove(room);
}