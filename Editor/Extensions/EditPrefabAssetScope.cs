using System;
using UnityEditor;
using UnityEngine;

public class EditPrefabAssetScope : IDisposable
{
    public readonly string assetPath;
    public readonly GameObject prefabRoot;
    public bool SavePrefabOnDispose { get; set; } = true;

    public EditPrefabAssetScope(string assetPath)
    {
        this.assetPath = assetPath;
        prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
    }

    public void Dispose()
    {
        if (SavePrefabOnDispose)
        {
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
        }
        PrefabUtility.UnloadPrefabContents(prefabRoot);
    }
}
