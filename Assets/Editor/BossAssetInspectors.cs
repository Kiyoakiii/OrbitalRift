#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace OrbitalRift
{
    [CustomEditor(typeof(BossAbilityDefinition))]
    public sealed class BossAbilityDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Этот ассет читает бой и Sandbox. Изменения полёта и таймингов проверяй на следующем запуске/касте. Цвета хвоста и частиц: открой ссылку Vfx или Shot → Vfx.", MessageType.Info);
            DrawPropertiesExcluding(serializedObject, "m_Script", "Summon", "Egg", "Beam", "Roots", "Gravity", "CastMovement");
            var behaviour = (BossAbilityBehaviour)serializedObject.FindProperty("Behaviour").enumValueIndex;
            if (serializedObject.FindProperty("UseCastMovement").boolValue) EditorGUILayout.PropertyField(serializedObject.FindProperty("CastMovement"), true);
            var extra = behaviour == BossAbilityBehaviour.Summon ? "Summon" : behaviour == BossAbilityBehaviour.RebirthEgg ? "Egg" : behaviour == BossAbilityBehaviour.Beam ? "Beam" : behaviour == BossAbilityBehaviour.Roots ? "Roots" : null;
            if (extra != null) EditorGUILayout.PropertyField(serializedObject.FindProperty(extra), true);
            if (behaviour == BossAbilityBehaviour.SandboxGravityWell) EditorGUILayout.PropertyField(serializedObject.FindProperty("Gravity"), true);
            if (serializedObject.ApplyModifiedProperties()) { EditorUtility.SetDirty(target); AssetDatabase.SaveAssetIfDirty(target); }
        }
    }
    [CustomEditor(typeof(BossDefinition))]
    public sealed class BossDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Главный ассет босса. Shots — выстрелы; Abilities — способности; Phases — выбор атак по оставшемуся HP. Двойной щелчок по ссылке открывает её настройки.", MessageType.Info);
            DrawDefaultInspector();
            if (GUILayout.Button("Проверить ссылки всех боссов"))
            {
                var errors = new System.Collections.Generic.List<string>(); BossAssetValidator.Validate(errors);
                if (errors.Count == 0) Debug.Log("Boss assets: ссылки и параметры корректны."); else Debug.LogError(string.Join("\n", errors));
            }
        }
    }
}
#endif
