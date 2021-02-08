using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using Unity.EditorCoroutines.Editor;
using UnityEditor.Compilation;
using TMPro;
using Button = UnityEngine.UIElements.Button;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using UnityEngine.UI;
using UnityEditor.UIElements;

namespace FabulousReplacer
{
    public class ComponentReplacer
    {
        private const string ADAPTER_PARENT_NAME = "{0}_TextAdaptersParent";
        private const string ADAPTER_GAMEOBJECT_NAME = "{0}_TMProAdapter";

        UpdatedReferenceAddressBook _updatedReferenceAddressBook;
        IntegerField lowRange;
        IntegerField highRange;
        TMP_FontAsset fontAsset;

        public ComponentReplacer(UpdatedReferenceAddressBook updatedReferenceAddressBook, Button updateComponentsButton, IntegerField lowRange, IntegerField highRange)
        {
            _updatedReferenceAddressBook = updatedReferenceAddressBook;
            this.lowRange = lowRange;
            this.highRange = highRange;

            updateComponentsButton.clicked += () =>
            {
                RunReplaceLogic();
            };

            //TODO REWORK
            fontAsset = AssetDatabase
                .LoadAssetAtPath("Packages/com.mariaheineboombyte.fabulous-text-replacer/TextMeshProFonts/Oswald/Oswald-SemiBold SDF.asset", typeof(TMP_FontAsset)) as TMP_FontAsset;
        }

        private void RunReplaceLogic()
        {
            int from = lowRange.value >= _updatedReferenceAddressBook.Count ? _updatedReferenceAddressBook.Count : lowRange.value;
            int to = highRange.value >= _updatedReferenceAddressBook.Count ? _updatedReferenceAddressBook.Count : highRange.value;

            for (int i = from; i < to; i++)
            {
                var references = _updatedReferenceAddressBook[i];

                foreach (UpdatedReference reference in references)
                {
                    ReplaceTextComponent(reference);
                }
            }
        }

        //
        // ─── TEXT COMPONENT REPLACEMENT ──────────────────────────────────
        //

        #region TEXT COMPONENT REPLACEMENT

        private void ReplaceTextComponent(UpdatedReference updatedReference)
        {
            TextInformation textInfo = updatedReference.textInformation;

            // * Don't even think of performing below operations on previously saved prefabs loaded into the memory
            // * They are like lost souls that want to trap your innocent code
            // * Whatever you execute on them gets lost in a limbo and flushed down along the garbage collection
            // * If you want to edit a prefab, make sure you just loaded it and you work on a fresh, crunchy instance

            Debug.Log($"<color=yellow>{updatedReference.prefabPath}</color>");
            

            using (var editScope = new EditPrefabAssetScope(updatedReference.prefabPath))
            {
                GameObject root = editScope.prefabRoot;
                TextMeshProUGUI tmProText = GetTMProText(updatedReference, textInfo, root);
                TMProAdapter tmProAdapter = CreateTextAdapter(updatedReference, root, tmProText);
                StyleTMProText(textInfo, tmProText);
                AssignTMProReference(updatedReference, tmProAdapter, root);
            }
        }

        private void AssignTMProReference(UpdatedReference reference, TMProAdapter tmProAdapter, GameObject root)
        {
            if (reference.isReferenced)
            {
                if (tmProAdapter == null)
                {
                    Debug.LogError($"Adapter is null for {reference.prefabPath} field {reference.fieldName}");
                }

                Type type = Type.GetType(reference.monoAssemblyName);

                Component mono = FabulousExtensions
                    .GetGameObjectAtAddress(root, reference.MonoAddress)
                    .GetComponent(type);

                if (mono == null)
                {
                    Debug.LogError($"Failed to find mono by its address for prefab: {root} at path {reference.prefabPath}");
                    return;
                }

                foreach (var field in type.GetFields(ReferenceFinder.FIELD_SEARCH_FLAGS))
                {
                    if (field.Name == $"{reference.fieldName}TMPro")
                    {
                        field.SetValue(mono, tmProAdapter.TMProText);
                    }
                    else if (field.Name == $"{reference.fieldName}Adapter")
                    {
                        field.SetValue(mono, tmProAdapter);
                    }
                }
            }
        }

        private void StyleTMProText(TextInformation textInfo, TextMeshProUGUI tmProText)
        {
            tmProText.text = textInfo.Text;
            tmProText.alignment = textInfo.TMProAlignment;
            tmProText.font = fontAsset;
            tmProText.fontSize = (float)textInfo.FontSize;
            tmProText.color = textInfo.FontColor;
            tmProText.enableWordWrapping = true;

            // TODO oki that is a small hack
            // using original font size as max size and always enabling auto sizing
            tmProText.fontSizeMax = textInfo.FontSize;
            //newText.enableAutoSizing = textInfo.AutoSize;
            tmProText.enableAutoSizing = true;

            tmProText.fontSizeMin = textInfo.MinSize;
            tmProText.richText = textInfo.IsRichText;
            tmProText.characterSpacing = -1.1f;
        }

        private static TextMeshProUGUI GetTMProText(UpdatedReference updatedReference, TextInformation textInfo, GameObject root)
        {
            TextMeshProUGUI newText;

            GameObject textParent = FabulousExtensions
                .GetGameObjectAtAddress(root, updatedReference.TextAddress);

            Text oldText = textParent.GetComponent<Text>();

            if (oldText != null)
            {
                UnityEngine.Object.DestroyImmediate(oldText, true);
                newText = textParent.AddComponent<TextMeshProUGUI>();
            }
            else
            {
                newText = FabulousExtensions
                    .GetGameObjectAtAddress(root, updatedReference.TextAddress)
                    .GetComponent<TextMeshProUGUI>();
            }

            return newText;
        }

        private static TMProAdapter CreateTextAdapter(UpdatedReference updatedReference, GameObject root, TextMeshProUGUI newTextComponent)
        {
            TMProAdapter adapter = null;

            if (updatedReference.isReferenced)
            {
                string monoTypeShortName = Type
                    .GetType(updatedReference.monoAssemblyName)
                    .Name;

                string adapterParentName = String.Format(ADAPTER_PARENT_NAME, monoTypeShortName);
                Transform adaptersParentTransform = root.transform.Find(adapterParentName);

                GameObject adaptersParent = null;
                if (adaptersParentTransform == null)
                {
                    adaptersParent = new GameObject(adapterParentName);
                    adaptersParent.transform.parent = root.transform;
                    adaptersParent.transform.SetAsLastSibling(); // * this is important because "adressing" part of the algorithm counts children index
                    adaptersParent.AddComponent<TMProAdapterParent>();
                }
                else
                {
                    adaptersParent = adaptersParentTransform.gameObject;
                }

                string adapterGameobjectName = String.Format(ADAPTER_GAMEOBJECT_NAME, updatedReference.fieldName);

                Transform adapterTransform = adaptersParent.transform.Find(adapterGameobjectName);

                if (adapterTransform == null)
                {
                    GameObject adapterGO = new GameObject(adapterGameobjectName);
                    adapterGO.transform.parent = adaptersParent.transform;
                    adapter = adapterGO.AddComponent<TMProAdapter>();
                    adapter.SetupAdapter(updatedReference.fieldName, newTextComponent);
                }
                else
                {
                    adapter = adaptersParent.GetComponent<TMProAdapterParent>()[updatedReference.fieldName];
                }
            }

            return adapter;
        }

        #endregion // TEXT COMPONENT REPLACEMENT
    }
}
