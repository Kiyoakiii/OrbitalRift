#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace OrbitalRift
{
    // The default inspector normally serializes ScriptableObject assets correctly,
    // but these profiles are also cloned by the live VFX preview.  Keep the
    // distinction visible and force a disk save after an actual profile edit.
    [CustomEditor(typeof(SpellVfxProfile))]
    [CanEditMultipleObjects]
    public sealed class SpellVfxProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawPersistenceBanner();

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            var changed = serializedObject.ApplyModifiedProperties();
            if (changed) SavePersistentTargets();

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Сохранить профиль на диск")) SavePersistentTargets();
        }

        private void DrawPersistenceBanner()
        {
            var profile = target as SpellVfxProfile;
            if (profile == null) return;
            var path = AssetDatabase.GetAssetPath(profile);
            if (!EditorUtility.IsPersistent(profile))
            {
                EditorGUILayout.HelpBox(
                    "Это временная preview-копия. Её изменения исчезнут после перезапуска. " +
                    "Выбери исходный .asset в Project и меняй его там.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox("Сохраняемый ассет: " + path, MessageType.Info);
        }

        private void SavePersistentTargets()
        {
            var saved = false;
            foreach (var item in targets)
            {
                if (!EditorUtility.IsPersistent(item)) continue;
                EditorUtility.SetDirty(item);
                saved = true;
            }
            if (saved) AssetDatabase.SaveAssets();
        }
    }

    [CustomEditor(typeof(ShieldVfxProfile))]
    [CanEditMultipleObjects]
    public sealed class ShieldVfxProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawPersistenceBanner();

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            var changed = serializedObject.ApplyModifiedProperties();
            if (changed) SavePersistentTargets();

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Сохранить профиль на диск")) SavePersistentTargets();
        }

        private void DrawPersistenceBanner()
        {
            var profile = target as ShieldVfxProfile;
            if (profile == null) return;
            var path = AssetDatabase.GetAssetPath(profile);
            if (!EditorUtility.IsPersistent(profile))
            {
                EditorGUILayout.HelpBox(
                    "Это временная preview-копия. Её изменения исчезнут после перезапуска. " +
                    "Выбери исходный .asset в Project и меняй его там.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox("Сохраняемый ассет: " + path, MessageType.Info);
        }

        private void SavePersistentTargets()
        {
            var saved = false;
            foreach (var item in targets)
            {
                if (!EditorUtility.IsPersistent(item)) continue;
                EditorUtility.SetDirty(item);
                saved = true;
            }
            if (saved) AssetDatabase.SaveAssets();
        }
    }

    [CustomEditor(typeof(BossVfxProfile))]
    [CanEditMultipleObjects]
    public sealed class BossVfxProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var path = AssetDatabase.GetAssetPath(target);
            if (!EditorUtility.IsPersistent(target))
            {
                EditorGUILayout.HelpBox(
                    "Это временная preview-копия. Её изменения исчезнут после перезапуска. " +
                    "Выбери исходный .asset в Project и меняй его там.",
                    MessageType.Warning);
            }
            else EditorGUILayout.HelpBox("Сохраняемый ассет: " + path, MessageType.Info);

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            var changed = serializedObject.ApplyModifiedProperties();
            if (changed) SavePersistentTargets();

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Сохранить профиль на диск")) SavePersistentTargets();
        }

        private void SavePersistentTargets()
        {
            var saved = false;
            foreach (var item in targets)
            {
                if (!EditorUtility.IsPersistent(item)) continue;
                EditorUtility.SetDirty(item);
                saved = true;
            }
            if (saved) AssetDatabase.SaveAssets();
        }
    }
}
#endif
