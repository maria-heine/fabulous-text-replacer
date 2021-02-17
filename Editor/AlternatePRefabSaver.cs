using System.Collections;
using System.Collections.Generic;
using FabulousReplacer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public class AlternatePRefabSaver : EditorWindow
{
    UpdatedReferenceAddressBook _updatedReferenceAddressBook;

    [MenuItem("Window/Zula Mobile/AlternatePRefabSaver _%#Y")]
    public static void ShowWindow()
    {
        // Opens the window, otherwise focuses it if it’s already open.
        var window = GetWindow<AlternatePRefabSaver>();

        // Adds a title to the window.
        window.titleContent = new GUIContent("AlternatePRefabSaverr");

        // Sets a minimum size to the window.
        window.minSize = new Vector2(250, 50);
    }

    private void OnEnable()
    {
        var root = rootVisualElement;

        Button initializeButton = new Button(() =>
        {
            SavePrefab();
        })
        { text = "Initialize" };
        root.Add(initializeButton);
    }

    private void SavePrefab()
    {
        string addressBookPath = "Packages/com.mariaheineboombyte.fabulous-text-replacer/Editor/Scriptable/UpdatedReferenceAddressBook.asset";
        UnityEngine.Object addressBook = AssetDatabase.LoadAssetAtPath(addressBookPath, typeof(UpdatedReferenceAddressBook));
        _updatedReferenceAddressBook = addressBook as UpdatedReferenceAddressBook;

        string assetPath = "Assets/RemoteAssets/DeeplyNested.prefab";

        using (var editScope = new EditPrefabAssetScope(assetPath))
        {
            GameObject root = editScope.prefabRoot;
            GameObject adaptersParent = new GameObject("AdaptersParent");
            adaptersParent.transform.parent = root.transform;
            adaptersParent.transform.SetAsFirstSibling();
        }
    }
}
