using UnityEngine;

namespace OrbitalRift.UI
{
    public readonly struct ExpeditionRoomNodeModel
    {
        public readonly Color Color;
        public readonly bool IsVisited;
        public readonly bool IsActive;

        public ExpeditionRoomNodeModel(Color color, bool isVisited, bool isActive)
        {
            Color = color;
            IsVisited = isVisited;
            IsActive = isActive;
        }
    }

    /// <summary>Read-only presentation data. The HUD never reads or changes gameplay objects directly.</summary>
    public sealed class ExpeditionHudModel
    {
        public bool Visible;
        public string RoomLabel = string.Empty;
        public string RunLabel = string.Empty;
        public string ObjectiveLabel = string.Empty;
        public string TrajectoryLabel = string.Empty;
        public string TickerLabel = string.Empty;
        public string ThreatTitle = string.Empty;
        public string ThreatValue = string.Empty;
        public string HullValue = string.Empty;
        public string IntroTitle = string.Empty;
        public string IntroSubtitle = string.Empty;
        public Color ThreatColor = Color.white;
        public Color ThreatEmptyColor = new Color(.16f, .025f, .045f, .92f);
        public Color HullColor = Color.white;
        public Color HullEmptyColor = new Color(.16f, .025f, .045f, .92f);
        public Color TrajectoryColor = Color.white;
        public Color TickerColor = Color.white;
        public Color IntroColor = Color.white;
        public int ThreatSegments;
        public int HullSegments;
        public int TotalHealthSegments = 12;
        public float IntroAlpha;
        public ExpeditionRoomNodeModel[] Rooms = System.Array.Empty<ExpeditionRoomNodeModel>();
    }
}
