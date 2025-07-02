using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DrivenKey))]
public class DrivenKeyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        DrivenKey script = (DrivenKey)target;

        if (script == null || script.keyframes == null) return;

        // 🚨 检查是否有超出 ±90° 的 driverValue
        bool outOfSafeRange = false;
        foreach (var frame in script.keyframes)
        {
            if (frame.driverValue < -90f || frame.driverValue > 90f)
            {
                outOfSafeRange = true;
                break;
            }
        }

        if (outOfSafeRange)
        {
            EditorGUILayout.HelpBox(
                "⚠️ 注意：已检测到关键帧 driver 角度超过 ±90 度。\n这可能会导致被驱动物体反转或跳转问题。",
                MessageType.Warning
            );
        }

        // 🚧 检查 drivenValues 长度与 drivenProperties 是否一致
        if (script.drivenProperties == null) return;
        int expectedLength = script.drivenProperties.Length;

        bool mismatchFound = false;
        foreach (var frame in script.keyframes)
        {
            if (frame.drivenValues == null || frame.drivenValues.Length != expectedLength)
            {
                mismatchFound = true;
                break;
            }
        }

        if (mismatchFound)
        {
            EditorGUILayout.HelpBox($"⚠️ 关键帧的 drivenValues 数量与 drivenProperties 不一致（应为 {expectedLength}）", MessageType.Warning);

            if (GUILayout.Button("自动修正所有关键帧长度"))
            {
                Undo.RecordObject(script, "Fix drivenValues Length");

                foreach (var frame in script.keyframes)
                {
                    float[] newValues = new float[expectedLength];
                    if (frame.drivenValues != null)
                    {
                        for (int i = 0; i < Mathf.Min(expectedLength, frame.drivenValues.Length); i++)
                            newValues[i] = frame.drivenValues[i];
                    }

                    frame.drivenValues = newValues;
                }

                EditorUtility.SetDirty(script);
            }
        }
    }
}
