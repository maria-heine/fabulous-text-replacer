using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FabulousReplacer
{
    public class ReferenceUpdater
    {
        const string TMPRO_TEXT_SUFFIX = "TMPro";

        UpdatedReferenceAddressBook _updatedReferenceAddressBook;

        public ReferenceUpdater(UpdatedReferenceAddressBook referenceAddressBook, Button updateReferencesButton)
        {
            _updatedReferenceAddressBook = referenceAddressBook;

            updateReferencesButton.clicked += () =>
            {
                RunUpdateReferencesLogic();
            };
        }

        void RunUpdateReferencesLogic()
        {
            foreach (var kvp in _updatedReferenceAddressBook)
            {
                try
                {
                    //! Super important, you need to save and reimport asset before closing edition
                    //! Otherwise a newly loaded asset will remove the past
                    // AssetDatabase.StartAssetEditing();

                    string prefabPath = kvp.Key;
                    Component prefab = AssetDatabase.LoadAssetAtPath(prefabPath, typeof(Component)) as Component;

                    Debug.Log($"<color=red>Updating for {prefabPath}</color>");

                    foreach (UpdatedReference reference in kvp.Value)
                    {
                        if (!reference.isReferenced)
                        {
                            continue;
                        }

                        Type type = Type.GetType(reference.monoAssemblyName);
                        Debug.Log($"<color=yellow>mono: {reference.monoAssemblyName} : {reference.fieldName}</color>");

                        Component usedPrefab;
                        string usedPath;

                        if (prefabPath == reference.prefabPath)
                        {
                            Debug.Log($"yas: {reference.rootPrefab} : {prefab.gameObject}");
                            usedPath = prefabPath;
                            usedPrefab = prefab;
                        }
                        else
                        {
                            Debug.Log($"nope: {reference.rootPrefab} : {prefab.gameObject}");
                            usedPath = reference.prefabPath;
                            usedPrefab = AssetDatabase.LoadAssetAtPath(usedPath, typeof(Component)) as Component;
                        }

                        Component mono = FabulousExtensions
                            .GetGameObjectAtAddress(usedPrefab.gameObject, reference.MonoAddress)
                            .GetComponent(type);

                        if (mono == null)
                        {
                            Debug.LogError($"Failed to find mono by its address for prefab: {usedPrefab} at path {usedPath}");
                            continue;
                        }

                        TextMeshProUGUI newTextComponent = FabulousExtensions
                            .GetGameObjectAtAddress(usedPrefab.gameObject, reference.ReferencedTextAddress)
                            .GetComponent<TextMeshProUGUI>();

                        if (newTextComponent == null)
                        {
                            Debug.LogError($"Failed to find TMPRO by its address for prefab: {usedPrefab} at path {usedPath}");
                            continue;
                        }

                        foreach (var field in type.GetFields(ReferenceFinder.FIELD_SEARCH_FLAGS))
                        {
                            if (field.Name == $"{reference.fieldName}{TMPRO_TEXT_SUFFIX}")
                            {
                                Debug.Log(field.Name);
                                Debug.Log(mono, mono.transform);
                                Debug.Log(newTextComponent, newTextComponent.transform);

                                field.SetValue(mono, newTextComponent);
                                Debug.Log(field.GetValue(mono));
                            }
                        }

                        // ? Consider moving that a step outside if the scirpt execution dies on production
                        AssetDatabase.SaveAssets();
                        // ! Super important line of code here
                        AssetDatabase.ForceReserializeAssets(new string[] { usedPath }, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
                        AssetDatabase.ImportAsset(usedPath);
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                        // AssetDatabase.StopAssetEditing();
                    }
                }
                finally
                {
                    // AssetDatabase.StopAssetEditing();
                }
            }
        }
    }
}
