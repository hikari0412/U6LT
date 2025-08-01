using UnityEditor;

public class AddEnableAddressablesDefine
{
    [MenuItem("Tools/修复 ENABLE_ADDRESSABLES 宏")]
    public static void FixAddressablesDefine()
    {
        var target = EditorUserBuildSettings.selectedBuildTargetGroup;
        string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
        if (!symbols.Contains("ENABLE_ADDRESSABLES"))
        {
            symbols += ";ENABLE_ADDRESSABLES";
            PlayerSettings.SetScriptingDefineSymbolsForGroup(target, symbols);
            UnityEngine.Debug.Log("已手动添加 ENABLE_ADDRESSABLES 宏！");
        }
        else
        {
            UnityEngine.Debug.Log("ENABLE_ADDRESSABLES 已存在，无需重复添加。");
        }
    }
}