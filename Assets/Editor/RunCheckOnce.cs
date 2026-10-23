using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class RunCheckOnce
{
    static RunCheckOnce()
    {
        EditorApplication.delayCall += () =>
        {
            CheckFireMaterialsDebug.RunCheck();
            AssetDatabase.DeleteAsset("Assets/Editor/RunCheckOnce.cs");
        };
    }
}
