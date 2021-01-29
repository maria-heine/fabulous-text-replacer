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
                string prefabPath = kvp.Key;
                Component prefab = AssetDatabase.LoadAssetAtPath(prefabPath, typeof(Component)) as Component;

                AssetDatabase.StartAssetEditing();

                foreach (UpdatedReference reference in kvp.Value)
                {
                    Type type = Type.GetType(reference.monoAssemblyName);

                    Component mono = FabulousExtensions
                        .GetGameObjectAtAddress(prefab.gameObject, reference.MonoAddress)
                        .GetComponent(type);

                    if (mono = null)
                    {
                        Debug.LogError($"Failed to find mono by its address for prefab: {prefab} at path {prefabPath}");
                    }

                    TextMeshProUGUI newTextComponent = FabulousExtensions
                        .GetGameObjectAtAddress(prefab.gameObject, reference.ReferencedTextAddress)
                        .GetComponent<TextMeshProUGUI>();

                    if (newTextComponent = null)
                    {
                        Debug.LogError($"Failed to find TMPRO by its address for prefab: {prefab} at path {prefabPath}");
                    }

                    foreach (var field in type.GetFields(ReferenceFinder.FIELD_SEARCH_FLAGS))
                    {
                        if (field.Name == $"{reference.fieldName}{TMPRO_TEXT_SUFFIX}")
                        {
                            Debug.Log(mono);
                            Debug.Log(newTextComponent);
                            
                            field.SetValue(mono, newTextComponent);
                        }
                    }
                }
                
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                // ! Super important line of code here
                AssetDatabase.ForceReserializeAssets(new string[] {prefabPath}, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata );
                AssetDatabase.ImportAsset(prefabPath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                // ? Consider moving that a step outside if the scirpt execution dies on production
            }
        }
    }
}
