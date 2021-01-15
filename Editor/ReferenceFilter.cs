using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Collections.Generic;
public class ReferenceFilter : EditorWindow
{
    [MenuItem("Assets/What objects use this?", false, 20)]
    private static void OnSearchForReferences()
    {
        string final = "";
        List<UnityEngine.Object> matches = new List<UnityEngine.Object>();
        int iid = Selection.activeInstanceID;
        if (AssetDatabase.IsMainAsset(iid))
        {
            // only main assets have unique paths
            string path = AssetDatabase.GetAssetPath(iid);
            // strip down the name
            final = System.IO.Path.GetFileNameWithoutExtension(path);
        }
        else
        {
            Debug.Log("Error Asset not found");
            return;
        }
        // get everything
        Object[] _Objects = Resources.FindObjectsOfTypeAll<Object>();

        //loop through everything
        foreach (Object go in _Objects)
        {
            // All objects
            Object[] depndencies = EditorUtility.CollectDependencies(new[] { go });
            foreach (Object o in depndencies)
                if (string.Compare(o.name.ToString(), final) == 0)
                    matches.Add(go);// add it to our list to highlight
        }
        Selection.objects = matches.ToArray();
        matches.Clear(); // clear the list 
    }
}