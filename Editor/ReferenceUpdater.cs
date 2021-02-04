using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FabulousReplacer
{
    public class ReferenceUpdater
    {
        const string TMPRO_TEXT_SUFFIX = "TMPro";

        UpdatedReferenceAddressBook _updatedReferenceAddressBook;
        IntegerField lowRange;
        IntegerField highRange;

        public ReferenceUpdater(UpdatedReferenceAddressBook referenceAddressBook, Button updateReferencesButton, IntegerField lowRange, IntegerField highRange)
        {
            _updatedReferenceAddressBook = referenceAddressBook;
            this.lowRange = lowRange;
            this.highRange = highRange;

            updateReferencesButton.clicked += () =>
            {
                RunUpdateReferencesLogic();
            };
        }

        void RunUpdateReferencesLogic()
        {
            //foreach (var kvp in _updatedReferenceAddressBook)
            //{

            int from = lowRange.value >= _updatedReferenceAddressBook.Count ? _updatedReferenceAddressBook.Count : lowRange.value;
            int to = highRange.value >= _updatedReferenceAddressBook.Count ? _updatedReferenceAddressBook.Count : highRange.value;

            for (int i = from; i < to; i++)
            {
                IEnumerable<UpdatedReference> references = _updatedReferenceAddressBook[i];
                string prefabPath = _updatedReferenceAddressBook.Paths[i];

                Component prefab = AssetDatabase.LoadAssetAtPath(prefabPath, typeof(Component)) as Component;

                Debug.Log($"<color=red>Updating for {prefabPath}</color>");

                foreach (UpdatedReference reference in references)
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
                        .GetGameObjectAtAddress(usedPrefab.gameObject, reference.TextAddress)
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
                    // ! Super important line of code here, otherwise nested prefabs get ffed up
                    AssetDatabase.ForceReserializeAssets(new string[] { usedPath }, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
                    AssetDatabase.ImportAsset(usedPath);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                }
            }
        }
    }
}
