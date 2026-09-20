using System.Collections.Generic;
using UnityEngine;

namespace OrbitalRift
{
    public enum LivingEncounterPhase { Arrival, Combat, Clear, Docking, Shop, Departing, Completed, Failed, RouteChoice, Reward }

    /// <summary>Offline M1/M2 slice. Pure phase ownership, independent of UI and transport.</summary>
    public sealed class LivingCosmosRunState
    {
        public const int RulesetVersion = 2;
        public SectorLayout Layout { get; private set; }
        public int PendingNodeId { get; private set; } = -1;
        public readonly List<int> Visited = new List<int>(8);
        public readonly HashSet<int> Cleared = new HashSet<int>();
        public int ShopChoicesRemaining { get; private set; }
        public int ClearedCount => Cleared.Count;
        public LivingEncounterPhase Phase { get; private set; }
        public int RoomIndex { get; private set; }
        public float PhaseSeconds { get; private set; }
        public float CombatSeconds { get; private set; }
        public int ClearSequence { get; private set; }
        public bool CanDealDamage => Phase == LivingEncounterPhase.Combat;
        public bool IsTerminal => Phase == LivingEncounterPhase.Completed || Phase == LivingEncounterPhase.Failed;
        public int Region => RegionForRoom(RoomIndex);
        public int NextRegion => PendingNodeId >= 0 ? RegionForRoom(PendingNodeId) : Region;
        public float Transition => Phase == LivingEncounterPhase.Departing
            ? Mathf.Clamp01(PhaseSeconds / StarStreamSettings.JumpDuration) : 0f;
        public float Jump => Phase == LivingEncounterPhase.Departing
            ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .16f, Transition)) *
              (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.32f, 1f, Transition))) : 0f;

        public static int RegionForRoom(int index) => index < 4 ? 0 : 1;
        public static string RegionName(int region) => region == 0 ? "СИНИЙ ПРЕДЕЛ" : "ЯНТАРНЫЙ ФРОНТ";

        public void EnterRoom(int index)
        {
            var previous = RoomIndex;
            if (Layout == null) Layout = CreatePreviewRoute(1);
            if (index < 0 || index >= Layout.Rooms.Count) throw new System.ArgumentOutOfRangeException(nameof(index));
            RoomIndex = index;
            PendingNodeId = -1;
            if (!Visited.Contains(index)) Visited.Add(index);
            ShopChoicesRemaining = Layout.Rooms[index].Type == SectorRoomType.Shop
                ? (Cleared.Contains(previous) && Layout.Rooms[previous].Type == SectorRoomType.Elite ? 2 : 1) : 0;
            CombatSeconds = 0f;
            SetPhase(LivingEncounterPhase.Arrival);
        }

        public void Initialize(int seed) { Layout = CreatePreviewRoute(seed); Visited.Clear(); Cleared.Clear(); ClearSequence = 0; EnterRoom(0); }
        public void OpenInitialNavigation()
        {
            if (RoomIndex != 0 || Phase != LivingEncounterPhase.Arrival) return;
            Cleared.Add(0);
            SetPhase(LivingEncounterPhase.RouteChoice);
        }
        public bool IsAvailable(int node) => Layout != null && !Cleared.Contains(node) &&
            Layout.Rooms[RoomIndex].Connections.Contains(node);
        public bool TryChoose(int node)
        {
            if (Phase != LivingEncounterPhase.RouteChoice || !IsAvailable(node)) return false;
            PendingNodeId = node;
            SetPhase(LivingEncounterPhase.Departing);
            return true;
        }
        public bool ConsumeShopChoice()
        {
            if (Phase != LivingEncounterPhase.Shop || ShopChoicesRemaining <= 0) return false;
            ShopChoicesRemaining--;
            return true;
        }
        // The reward screen owns its input, so clearing cannot continue until it has
        // committed the deterministic choice/checkpoint.
        public bool BeginReward()
        {
            if (Phase != LivingEncounterPhase.Combat) return false;
            SetPhase(LivingEncounterPhase.Reward);
            return true;
        }
        public bool ContinueAfterReward()
        {
            if (Phase != LivingEncounterPhase.Reward) return false;
            SetPhase(LivingEncounterPhase.Clear);
            return true;
        }

        // Returns true exactly once per departure. The caller enters the next room immediately.
        public bool Tick(float dt, int enemyHealth, int hullHealth, float arrivalRemaining,
            bool docking, bool shopOpen, bool lastRoom)
        {
            if (IsTerminal || dt <= 0f) return false;
            if (hullHealth <= 0) { SetPhase(LivingEncounterPhase.Failed); return false; }
            PhaseSeconds += dt;
            switch (Phase)
            {
                case LivingEncounterPhase.Arrival:
                    if (docking) SetPhase(LivingEncounterPhase.Docking);
                    else if (arrivalRemaining <= 0f)
                        SetPhase(enemyHealth > 0 ? LivingEncounterPhase.Combat : LivingEncounterPhase.Clear);
                    break;
                case LivingEncounterPhase.Combat:
                    CombatSeconds += dt;
                    if (enemyHealth <= 0) { ClearSequence++; SetPhase(LivingEncounterPhase.Clear); }
                    break;
                case LivingEncounterPhase.Docking:
                case LivingEncounterPhase.Shop:
                    if (shopOpen && Phase != LivingEncounterPhase.Shop) SetPhase(LivingEncounterPhase.Shop);
                    else if (!docking && !shopOpen) SetPhase(LivingEncounterPhase.Clear);
                    break;
                case LivingEncounterPhase.Clear:
                    if (PhaseSeconds >= CoopRoomRules.RoomClearDelay)
                    {
                        Cleared.Add(RoomIndex);
                        SetPhase(lastRoom ? LivingEncounterPhase.Completed : LivingEncounterPhase.RouteChoice);
                    }
                    break;
                case LivingEncounterPhase.Departing:
                    if (PhaseSeconds >= StarStreamSettings.JumpDuration)
                    {
                        // Disarm the return event, including if the caller misses EnterRoom.
                        SetPhase(LivingEncounterPhase.Arrival);
                        return true;
                    }
                    break;
            }
            return false;
        }

        public LivingCosmosRunCheckpoint CreateCheckpoint()
        {
            return new LivingCosmosRunCheckpoint {
                version=LivingCosmosRunCheckpoint.CurrentVersion, seed=Layout == null ? 0 : Layout.Seed,
                roomIndex=RoomIndex, pendingNodeId=PendingNodeId, phase=(int)Phase, phaseMilliseconds=Mathf.RoundToInt(PhaseSeconds*1000f),
                combatMilliseconds=Mathf.RoundToInt(CombatSeconds*1000f), clearSequence=ClearSequence,
                shopChoicesRemaining=ShopChoicesRemaining, visited=new List<int>(Visited), cleared=new List<int>(Cleared)
            };
        }

        public void Restore(LivingCosmosRunCheckpoint checkpoint)
        {
            if (checkpoint == null || checkpoint.version != LivingCosmosRunCheckpoint.CurrentVersion || checkpoint.seed == 0 ||
                checkpoint.roomIndex < 0 || checkpoint.roomIndex >= 8 || checkpoint.phaseMilliseconds < 0 ||
                checkpoint.combatMilliseconds < 0 || checkpoint.clearSequence < 0 || checkpoint.shopChoicesRemaining < 0 ||
                checkpoint.shopChoicesRemaining > 2 || checkpoint.visited == null || checkpoint.cleared == null)
                throw new System.ArgumentException("Invalid living cosmos checkpoint", nameof(checkpoint));
            Layout=CreatePreviewRoute(checkpoint.seed); Visited.Clear(); Cleared.Clear();
            for (var i=0;i<checkpoint.visited.Count;i++)
                if (checkpoint.visited[i] < 0 || checkpoint.visited[i] >= Layout.Rooms.Count || Visited.Contains(checkpoint.visited[i])) throw new System.ArgumentException("Invalid visited node");
                else Visited.Add(checkpoint.visited[i]);
            for (var i=0;i<checkpoint.cleared.Count;i++)
                if (checkpoint.cleared[i] < 0 || checkpoint.cleared[i] >= Layout.Rooms.Count || !Cleared.Add(checkpoint.cleared[i])) throw new System.ArgumentException("Invalid cleared node");
            if (!Visited.Contains(checkpoint.roomIndex) || !Layout.Rooms[checkpoint.roomIndex].Connections.Contains(checkpoint.pendingNodeId) && checkpoint.pendingNodeId >= 0)
                throw new System.ArgumentException("Invalid current route position");
            var phase=(LivingEncounterPhase)checkpoint.phase;
            if (!System.Enum.IsDefined(typeof(LivingEncounterPhase),phase) || phase == LivingEncounterPhase.Completed || phase == LivingEncounterPhase.Failed ||
                phase == LivingEncounterPhase.Departing || phase == LivingEncounterPhase.RouteChoice || phase == LivingEncounterPhase.Shop || phase == LivingEncounterPhase.Docking)
                throw new System.ArgumentException("Unsupported checkpoint phase");
            if (phase != LivingEncounterPhase.Reward) throw new System.ArgumentException("Only reward checkpoints are resumable in V0");
            RoomIndex=checkpoint.roomIndex; PendingNodeId=checkpoint.pendingNodeId; Phase=phase;
            PhaseSeconds=checkpoint.phaseMilliseconds/1000f; CombatSeconds=checkpoint.combatMilliseconds/1000f;
            ClearSequence=checkpoint.clearSequence; ShopChoicesRemaining=checkpoint.shopChoicesRemaining;
        }

        private void SetPhase(LivingEncounterPhase phase) { Phase = phase; PhaseSeconds = 0f; }

        public static SectorLayout CreatePreviewRoute(int seed)
        {
            var types = new[] { SectorRoomType.Start, SectorRoomType.Combat, SectorRoomType.Elite,
                SectorRoomType.Shop, SectorRoomType.Combat, SectorRoomType.Elite, SectorRoomType.Shop, SectorRoomType.Boss };
            var depths = new[] { 0,1,1,2,3,3,4,5 };
            var links = new[] { new[]{1,2}, new[]{3}, new[]{3}, new[]{4,5}, new[]{6}, new[]{6}, new[]{7}, new int[0] };
            var rooms = new List<SectorRoom>(types.Length);
            for (var i = 0; i < types.Length; i++)
            {
                var room = new SectorRoom(i, types[i], depths[i], Mathf.Max(1, depths[i] + (types[i] == SectorRoomType.Elite ? 1 : 0)));
                room.Connections.AddRange(links[i]);
                rooms.Add(room);
            }
            return new SectorLayout(seed, rooms);
        }

        public static string NodeName(int id)
        {
            switch(id) {
                case 0:return "ТОЧКА ВХОДА"; case 1:return "ТИХАЯ ОРБИТА";
                case 2:return "ГРОЗОВОЙ ПОЯС"; case 3:return "СТАНЦИЯ «ЭХО»";
                case 4:return "ДАЛЬНИЙ ПАТРУЛЬ"; case 5:return "КРАСНЫЙ КОРИДОР";
                case 6:return "ПОСЛЕДНИЙ ДОК"; default:return "СТРАЖ РАЗЛОМА";
            }
        }
        public static Vector2 MapPosition(SectorLayout layout, int id)
        {
            var y = id==1 || id==4 ? .70f : id==2 || id==5 ? .30f : .5f;
            if ((layout.Seed & 1) != 0) y=1f-y;
            return new Vector2(.075f + layout.Rooms[id].Depth * .17f, y);
        }
        public static bool ValidateRoute(SectorLayout layout)
        {
            if (layout==null || layout.Rooms.Count!=8) return false;
            var reachable=new bool[8]; reachable[0]=true;
            for(var i=0;i<8;i++)
            {
                if (!reachable[i] || layout.Rooms[i].Id!=i) return false;
                foreach(var next in layout.Rooms[i].Connections)
                {
                    if(next<=i || next>=8 || layout.Rooms[next].Depth<=layout.Rooms[i].Depth) return false;
                    reachable[next]=true;
                }
                if (i<7 && layout.Rooms[i].Connections.Count==0) return false;
            }
            return layout.Rooms[7].Type==SectorRoomType.Boss && layout.Rooms[7].Connections.Count==0;
        }
    }

    [System.Serializable]
    public sealed class LivingCosmosRunCheckpoint
    {
        public const int CurrentVersion=1;
        public int version=CurrentVersion, seed, roomIndex, pendingNodeId=-1, phase, phaseMilliseconds, combatMilliseconds,
            clearSequence, shopChoicesRemaining;
        public List<int> visited=new List<int>();
        public List<int> cleared=new List<int>();
    }
}
