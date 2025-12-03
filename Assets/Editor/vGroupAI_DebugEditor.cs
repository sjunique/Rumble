#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(DenvzlaEstudio.vGroupAI))]
public class vGroupAI_DebugEditor : Editor
{
    SerializedProperty nameGroup, useDropRate, boss, aiBoss, aiPrefabs;

    void OnEnable()
    {
        nameGroup  = serializedObject.FindProperty("NameGroup");
        useDropRate= serializedObject.FindProperty("UseDropRate");
        boss       = serializedObject.FindProperty("Boss");
        aiBoss     = serializedObject.FindProperty("AIBoss");
        aiPrefabs  = serializedObject.FindProperty("AIPrefab");
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Debug editor (temporary). Prefer the Advanced Spawn System Manager.", MessageType.Info);
        serializedObject.Update();
        EditorGUILayout.PropertyField(nameGroup);
        EditorGUILayout.PropertyField(useDropRate);
        EditorGUILayout.PropertyField(boss);
        if (boss.boolValue) EditorGUILayout.PropertyField(aiBoss);
        EditorGUILayout.PropertyField(aiPrefabs, true); // <- drag your AI prefabs with vFsmAI here
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
