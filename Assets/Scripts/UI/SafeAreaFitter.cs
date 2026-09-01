using UnityEngine;

namespace OrbitalRift.UI
{
    /// <summary>
    /// Keeps authored UI inside notches, rounded corners and Android navigation areas.
    /// Add gameplay widgets below this transform and edit them normally with RectTransform anchors.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void OnEnable()
        {
            rectTransform = GetComponent<RectTransform>();
            Apply(true);
        }

        private void Update()
        {
            Apply(false);
        }

        private void Apply(bool force)
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            var screenSize = new Vector2Int(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            var safeArea = Screen.safeArea;
            if (!force && safeArea == lastSafeArea && screenSize == lastScreenSize) return;

            lastSafeArea = safeArea;
            lastScreenSize = screenSize;
            var minimum = safeArea.position;
            var maximum = safeArea.position + safeArea.size;
            minimum.x /= screenSize.x;
            minimum.y /= screenSize.y;
            maximum.x /= screenSize.x;
            maximum.y /= screenSize.y;
            rectTransform.anchorMin = minimum;
            rectTransform.anchorMax = maximum;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
