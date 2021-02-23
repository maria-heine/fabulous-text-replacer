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
using System.Reflection;
using System.Linq;

namespace FabulousReplacer
{
    public class ComponentReplacer
    {
        private const string ADAPTER_PARENT_NAME = "{0}_TextAdaptersParent";
        private const string ADAPTER_GAMEOBJECT_NAME = "TMProAdapter_{0}";

        UpdatedReferenceAddressBook _updatedReferenceAddressBook;
        TMP_FontAsset fontAsset;

        public ComponentReplacer(UpdatedReferenceAddressBook updatedReferenceAddressBook, Button updateComponentsButton)
        {
            _updatedReferenceAddressBook = updatedReferenceAddressBook;

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
            // foreach (var reference in _updatedReferenceAddressBook)
            // {
            //     List<ReplaceUnit> referenceGroup = reference.Value;

            //     foreach (ReplaceUnit replaceUnit in referenceGroup)
            //     {
            //         ReplaceTextComponent(replaceUnit);
            //     }
            // }

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var reference in _updatedReferenceAddressBook)
                {
                    List<ReplaceUnit> referenceGroup = reference.Value;

                    foreach (ReplaceUnit replaceUnit in referenceGroup)
                    {
                        ReplaceTextComponent(replaceUnit);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        //
        // ─── TEXT COMPONENT REPLACEMENT ──────────────────────────────────
        //

        #region TEXT COMPONENT REPLACEMENT

        private void ReplaceTextComponent(ReplaceUnit updatedReference)
        {
            TextInformation textInfo = updatedReference.textInformation;

            // * Don't even think of performing below operations on previously saved prefabs loaded into the memory
            // * They are like lost souls that want to trap your innocent code
            // * Whatever you execute on them gets lost in a limbo and flushed down along the garbage collection
            // * If you want to edit a prefab, make sure you just loaded it and you work on a fresh, crunchy instance

            using (var editScope = new EditPrefabAssetScope(updatedReference.prefabPath))
            {
                GameObject root = editScope.prefabRoot;
                TextMeshProUGUI tmProText = GetTMProText(updatedReference, textInfo, root);
                StyleTMProText(textInfo, tmProText);

                if (updatedReference.isReferenced)
                {
                    TMProAdapter tmProAdapter = CreateTextAdapter(updatedReference, root, tmProText);
                    AssignTMProReference(updatedReference, tmProAdapter, root);
                }
            }
        }

        private static TextMeshProUGUI GetTMProText(ReplaceUnit updatedReference, TextInformation textInfo, GameObject root)
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

            if (newText == null)
            {
                Debug.LogError($"TMPro text component is null");
            }

            return newText;
        }

        private static TMProAdapter CreateTextAdapter(ReplaceUnit updatedReference, GameObject root, TextMeshProUGUI newTextComponent)
        {
            TMProAdapter adapter = null;

            string fieldOwnerName = updatedReference.fieldInformation.FieldOwnerType.Name;
            string adapterParentName = String.Format(ADAPTER_PARENT_NAME, fieldOwnerName);

            GameObject adaptersParent = GetAdaptersParent(root, adapterParentName);
            adapter = GetAdapter(updatedReference, newTextComponent, adaptersParent);

            return adapter;
        }
        private static GameObject GetAdaptersParent(GameObject root, string adapterParentName)
        {
            GameObject adaptersParent;
            Transform adaptersParentTransform = root.transform.Find(adapterParentName);

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

            return adaptersParent;
        }

        private static TMProAdapter GetAdapter(ReplaceUnit updatedReference, TextMeshProUGUI newTextComponent, GameObject adaptersParent)
        {
            TMProAdapter adapter;
            string fieldName = GetAdapterGameObjectName(updatedReference.fieldInformation);
            string adapterName = String.Format(ADAPTER_GAMEOBJECT_NAME, fieldName);

            Transform adapterTransform = adaptersParent.transform.Find(adapterName);

            if (adapterTransform == null)
            {
                adapter = CreateNewAdapter(newTextComponent, adaptersParent, fieldName, adapterName);
            }
            else
            {
                Debug.LogError($"Dos that ever happen and should it even, maybe in case of repeated reference");

                //TODO add version with FieldInformation
                adapter = adaptersParent.GetComponent<TMProAdapterParent>()[fieldName];
            }

            return adapter;
        }

        private static TMProAdapter CreateNewAdapter(TextMeshProUGUI newTextComponent, GameObject adaptersParent, string fieldName, string adapterGameobjectName)
        {
            TMProAdapter adapter;
            GameObject adapterGO = new GameObject(adapterGameobjectName);
            adapterGO.transform.parent = adaptersParent.transform;
            adapter = adapterGO.AddComponent<TMProAdapter>();
            adapter.SetupAdapter(fieldName, newTextComponent);
            return adapter;
        }

        private static string GetAdapterGameObjectName(FieldInformation fieldInformation)
        {
            string adapterName = "";

            if (fieldInformation.FieldType.HasOneOfTheFlags(FieldType.Nested | FieldType.External))
            {
                var eofi = fieldInformation.GetFieldInformationParamter<ExternallyOwnedFieldInformation>();

                adapterName += $"{eofi.fieldInformation.FieldName}_";

                if (eofi.fieldInformation.FieldType.HasOneOfTheFlags(FieldType.Arrayed | FieldType.Listed))
                {
                    var efi = eofi.fieldInformation.GetFieldInformationParamter<EnumerableFieldInformation>();
                    adapterName += $"i{efi.index}_";
                }
            }

            adapterName += fieldInformation.FieldName;

            if (fieldInformation.FieldType.HasOneOfTheFlags(FieldType.Arrayed | FieldType.Listed))
            {
                adapterName += $"_i{fieldInformation.GetFieldInformationParamter<EnumerableFieldInformation>().index}";
            }

            return adapterName;
        }

        private void AssignTMProReference(ReplaceUnit reference, TMProAdapter tmProAdapter, GameObject root)
        {
            if (tmProAdapter == null)
            {
                Debug.LogError($"Adapter is null for {reference.prefabPath} field {reference.fieldName}");
            }

            object fieldOwner = GetFieldOwner(reference, root);

            GetAdapterAndTmproFieldInfos(
                reference.fieldInformation,
                out FieldInfo adapterFieldInfo,
                out FieldInfo tmProFieldInfo);

            SetAdapterAndTMProFieldValues(tmProAdapter, reference, fieldOwner, adapterFieldInfo, tmProFieldInfo);
        }

        private static object GetFieldOwner(ReplaceUnit replaceUnit, GameObject root)
        {
            object fieldOwner = null;
            FieldInformation fieldInformation = replaceUnit.fieldInformation;

            Type monoType = fieldInformation.FieldOwnerType;
            Component mono = FabulousExtensions
                .GetGameObjectAtAddress(root, replaceUnit.MonoAddress)
                .GetComponent(monoType);

            if (mono == null)
            {
                throw new NullReferenceException($"Failed to find mono by its address for prefab: {root} at path {replaceUnit.prefabPath}");
            }

            if (fieldInformation.FieldType.HasOneOfTheFlags(FieldType.Nested | FieldType.External))
            {
                ExternallyOwnedFieldInformation eofi = fieldInformation.GetFieldInformationParamter<ExternallyOwnedFieldInformation>();

                FieldInfo externalObjectFieldInfo = monoType.GetField(eofi.ExternalOwnerFieldName, ReferenceFinder.GENEROUS_NONSTATIC_FIELD_SEARCH_FLAGS);

                if (eofi.fieldInformation.FieldType.HasOneOfTheFlags(FieldType.Arrayed | FieldType.Listed))
                {
                    var efi = eofi.fieldInformation.GetFieldInformationParamter<EnumerableFieldInformation>();
                    IEnumerable enumberableOwner = (IEnumerable)externalObjectFieldInfo.GetValue(mono);

                    int index = 0;
                    foreach (var item in enumberableOwner)
                    {
                        if (index == efi.index)
                        {
                            fieldOwner = item;
                        }
                        index++;
                    }
                }
                else
                {
                    fieldOwner = externalObjectFieldInfo.GetValue(mono);
                }
            }
            else
            {
                fieldOwner = mono;
            }

            return fieldOwner;
        }

        private static void SetAdapterAndTMProFieldValues(TMProAdapter tmProAdapter, ReplaceUnit replaceUnit, object fieldOwner, FieldInfo adapterFieldInfo, FieldInfo tmProFieldInfo)
        {
            FieldInformation fieldInformation = replaceUnit.fieldInformation;
            FieldType fieldType = fieldInformation.FieldType;

            if (fieldType.HasOneOfTheFlags(FieldType.Listed | FieldType.Arrayed))
            {
                var adpaterField = adapterFieldInfo.GetValue(fieldOwner);
                var tmProField = tmProFieldInfo.GetValue(fieldOwner);

                EnumerableFieldInformation efi = fieldInformation.GetFieldInformationParamter<EnumerableFieldInformation>();

                if (adpaterField != null && tmProField != null)
                {
                    if (adpaterField is List<TMProAdapter> adapterList)
                    {
                        adapterList[efi.index] = tmProAdapter;
                        (tmProField as List<TextMeshProUGUI>).Add(tmProAdapter.TMProText);
                    }
                    else if (adpaterField is TMProAdapter[] adapterArray)
                    {
                        adapterArray[efi.index] = tmProAdapter;

                        //* We don't need to do the same with above adapterArray since it already has the correct size as it
                        //* replaced the old array of text components
                        TextMeshProUGUI[] tmproFieldArray = tmProField as TextMeshProUGUI[];
                        TextMeshProUGUI[] newTMProFieldArray = new TextMeshProUGUI[tmproFieldArray.Length + 1];
                        tmproFieldArray.CopyTo(newTMProFieldArray, 0);
                        newTMProFieldArray[tmproFieldArray.Length] = tmProAdapter.TMProText;
                        tmProFieldInfo.SetValue(fieldOwner, newTMProFieldArray);
                    }
                    else
                    {
                        Debug.LogError($"Huh? Thats weird");
                    }
                }
                else
                {
                    Debug.LogError($"Either adapter field: {adpaterField} or tmpro field: {tmProField} is null.");
                }
            }
            else
            {
                adapterFieldInfo.SetValue(fieldOwner, tmProAdapter);
                tmProFieldInfo.SetValue(fieldOwner, tmProAdapter.TMProText);
            }
        }

        private static void GetAdapterAndTmproFieldInfos(
            FieldInformation fieldInformation,
            out FieldInfo adapterFieldInfo,
            out FieldInfo tmProFieldInfo)
        {
            Type type = Type.GetType(fieldInformation.FieldDefiningTypeAssemblyName);
            adapterFieldInfo = type.GetField(fieldInformation.FieldName, ReferenceFinder.GENEROUS_NONSTATIC_FIELD_SEARCH_FLAGS);
            tmProFieldInfo = type.GetField($"{fieldInformation.FieldName}TMPro", ReferenceFinder.GENEROUS_NONSTATIC_FIELD_SEARCH_FLAGS);

            if (adapterFieldInfo == null || tmProFieldInfo == null)
            {
                Debug.Log($"Either adapterFieldinfo: {adapterFieldInfo} or tmprofieldinfo: {tmProFieldInfo} is still null");
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

        #endregion // TEXT COMPONENT REPLACEMENT
    }
}
