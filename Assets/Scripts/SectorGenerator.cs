using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OrbitalRift
{
    public enum SectorRoomType : byte { Start, Combat, Elite, Event, Shop, Boss }

    public sealed class SectorRoom
    {
        public readonly int Id;
        public readonly SectorRoomType Type;
        public readonly int Depth;
        public readonly int Threat;
        public readonly List<int> Connections = new List<int>(4);

        public SectorRoom(int id, SectorRoomType type, int depth, int threat)
        {
            Id = id;
            Type = type;
            Depth = depth;
            Threat = threat;
        }
    }

    /// <summary>Pure shared data: the same seed produces the same graph on every device.</summary>
    public sealed class SectorLayout
    {
        public readonly int Seed;
        public readonly IReadOnlyList<SectorRoom> Rooms;

        public SectorLayout(int seed, IReadOnlyList<SectorRoom> rooms)
        {
            Seed = seed;
            Rooms = rooms;
        }

        public string Signature()
        {
            var builder = new StringBuilder(Rooms.Count * 20);
            builder.Append(Seed).Append('|');
            for (var i = 0; i < Rooms.Count; i++)
            {
                var room = Rooms[i];
                builder.Append(room.Id).Append(':').Append((int)room.Type).Append(':')
                    .Append(room.Depth).Append(':').Append(room.Threat).Append('[');
                for (var link = 0; link < room.Connections.Count; link++)
                    builder.Append(room.Connections[link]).Append(',');
                builder.Append("];\n");
            }
            return builder.ToString();
        }
    }

    public static class SectorGenerator
    {
        public const int MinMainPathRooms = 5;
        public const int MaxMainPathRooms = 16;
        public const int MaxRooms = 24;

        public static SectorLayout Generate(int seed, int mainPathRooms = 10)
        {
            mainPathRooms = Mathf.Clamp(mainPathRooms, MinMainPathRooms, MaxMainPathRooms);
            var random = new System.Random(seed);
            var rooms = new List<SectorRoom>(MaxRooms);

            // Create side rooms before the final encounter. Expedition runs
            // advance by room index, therefore the boss must be the actual
            // final room rather than merely the last node on the main path.
            for (var depth = 0; depth < mainPathRooms - 1; depth++)
            {
                var type = depth == 0 ? SectorRoomType.Start : RollMainPathType(random, depth);
                rooms.Add(new SectorRoom(rooms.Count, type, depth, Mathf.Clamp(depth + random.Next(-1, 2), 1, 20)));
                if (depth > 0) Connect(rooms, depth - 1, depth);
            }

            var branchCount = Mathf.Clamp(mainPathRooms / 3 + 1, 2, MaxRooms - mainPathRooms);
            for (var branch = 0; branch < branchCount; branch++)
            {
                var anchor = random.Next(1, mainPathRooms - 2);
                var id = rooms.Count;
                rooms.Add(new SectorRoom(id, RollBranchType(random), anchor + 1,
                    Mathf.Clamp(anchor + random.Next(0, 3), 1, 20)));
                Connect(rooms, anchor, id);

                if (random.NextDouble() < .55)
                    Connect(rooms, id, random.Next(anchor + 1, mainPathRooms - 1));
            }

            var bossDepth = mainPathRooms - 1;
            var bossThreat = Mathf.Clamp(bossDepth + random.Next(-1, 2), 1, 20);
            var bossId = rooms.Count;
            rooms.Add(new SectorRoom(bossId, SectorRoomType.Boss, bossDepth, bossThreat));
            Connect(rooms, mainPathRooms - 2, bossId);

            for (var i = 0; i < rooms.Count; i++) rooms[i].Connections.Sort();
            return new SectorLayout(seed, rooms);
        }

        public static bool Validate(SectorLayout layout, out string error)
        {
            error = string.Empty;
            if (layout == null || layout.Rooms == null || layout.Rooms.Count < MinMainPathRooms || layout.Rooms.Count > MaxRooms)
            {
                error = "Sector room count is outside safe bounds.";
                return false;
            }

            var startCount = 0;
            var bossCount = 0;
            for (var i = 0; i < layout.Rooms.Count; i++)
            {
                var room = layout.Rooms[i];
                if (room == null || room.Id != i)
                {
                    error = "Sector room identifiers are not contiguous.";
                    return false;
                }
                if (room.Type == SectorRoomType.Start) startCount++;
                if (room.Type == SectorRoomType.Boss) bossCount++;
                for (var linkIndex = 0; linkIndex < room.Connections.Count; linkIndex++)
                {
                    var link = room.Connections[linkIndex];
                    if (link < 0 || link >= layout.Rooms.Count || link == room.Id ||
                        !layout.Rooms[link].Connections.Contains(room.Id))
                    {
                        error = "Sector contains an invalid or one-way connection.";
                        return false;
                    }
                }
            }

            if (startCount != 1 || bossCount != 1)
            {
                error = "Sector must contain exactly one start and one boss.";
                return false;
            }
            if (layout.Rooms[layout.Rooms.Count - 1].Type != SectorRoomType.Boss)
            {
                error = "Sector boss must be the final room.";
                return false;
            }

            var visited = new bool[layout.Rooms.Count];
            var queue = new Queue<int>();
            visited[0] = true;
            queue.Enqueue(0);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var links = layout.Rooms[current].Connections;
                for (var i = 0; i < links.Count; i++)
                    if (!visited[links[i]])
                    {
                        visited[links[i]] = true;
                        queue.Enqueue(links[i]);
                    }
            }

            for (var i = 0; i < visited.Length; i++)
                if (!visited[i])
                {
                    error = "Sector contains an unreachable room.";
                    return false;
                }
            return true;
        }

        private static SectorRoomType RollMainPathType(System.Random random, int depth)
        {
            var roll = random.Next(100);
            if (depth > 2 && roll < 14) return SectorRoomType.Elite;
            if (roll < 29) return SectorRoomType.Event;
            if (roll < 38) return SectorRoomType.Shop;
            return SectorRoomType.Combat;
        }

        private static SectorRoomType RollBranchType(System.Random random)
        {
            var roll = random.Next(100);
            if (roll < 32) return SectorRoomType.Event;
            if (roll < 55) return SectorRoomType.Elite;
            if (roll < 72) return SectorRoomType.Shop;
            return SectorRoomType.Combat;
        }

        private static void Connect(List<SectorRoom> rooms, int first, int second)
        {
            if (!rooms[first].Connections.Contains(second)) rooms[first].Connections.Add(second);
            if (!rooms[second].Connections.Contains(first)) rooms[second].Connections.Add(first);
        }
    }
}
