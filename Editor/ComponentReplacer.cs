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

namespace FabulousReplacer
{
    public class ComponentReplacer
    {
        UpdatedReferenceAddressBook _updatedReferenceAddressBook;
        Dictionary<Type, List<string>> _updatedMonoFields;
        Dictionary<string, Component> _reloadedComponents;

        Dictionary<Type, MonoUpdateDetails> _monoUpdateDetails;

        private class MonoUpdateDetails
        {
            public List<string> fieldNames;
        }

        TMP_FontAsset fontAsset;

        public ComponentReplacer(UpdatedReferenceAddressBook updatedReferenceAddressBook, Button updateComponentsButton)
        {
            _updatedReferenceAddressBook = updatedReferenceAddressBook;
            _updatedMonoFields = new Dictionary<Type, List<string>>();
            _reloadedComponents = new Dictionary<string, Component>();

            updateComponentsButton.clicked += () =>
            {
                RunReplaceLogic();
            };

            //TODO REWORK
            fontAsset = AssetDatabase
                .LoadAssetAtPath("Packages/com.mariaheineboombyte.fabulous-text-replacer/TextMeshProFonts/Oswald/Oswald-Regular SDF.asset", typeof(TMP_FontAsset)) as TMP_FontAsset;
        }

        // TODO What about private Text fields?
        // ! I am still missing the case of text components that dont have references at all
        private void RunReplaceLogic()
        {
            List<string> toReserailize = new List<string>();

            foreach (var kvp in _updatedReferenceAddressBook)
            {
                string prefabPath = kvp.Key;

                foreach (UpdatedReference reference in kvp.Value)
                {
                    GatherMono(reference);

                    // * Step: Replace component
                    ReplaceTextComponent(reference);
                }
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                MassReplaceFields();
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message + ex.StackTrace);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            CompilationPipeline.RequestScriptCompilation();

            // CompilationPipeline.compilationFinished += stuff =>
            // {

            // };

            // CompilationPipeline.RequestScriptCompilation();
        }

        //
        // ─── SCRIPT REPLACEMENT ──────────────────────────────────────────
        //

        #region SCRIPT REPLACEMENT 

        private void GatherMono(UpdatedReference reference)
        {
            if (reference.isReferenced)
            {
                Type monoType = reference.MonoType;
                string fieldName = reference.fieldName;

                if (_updatedMonoFields.ContainsKey(monoType) && _updatedMonoFields[monoType].Contains(fieldName))
                {
                    return;
                }
                else if (!_updatedMonoFields.ContainsKey(monoType))
                {
                    _updatedMonoFields[monoType] = new List<string>();
                }

                _updatedMonoFields[monoType].Add(fieldName);
            }
        }

        private void MassReplaceFields()
        {
            foreach (var item in _updatedMonoFields)
            {
                Type monoType = item.Key;
                List<string> fieldNames = item.Value;

                string scriptFileName = monoType.Name;
                string[] assets = AssetDatabase.FindAssets($"{scriptFileName}");
                var path = AssetDatabase.GUIDToAssetPath(assets[0]);

                if (assets.Length != 1)
                {
                    Debug.LogError("Well, really we shouldn't find less or more than exactly one asset like that");
                }
                else if (AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(UnityEditor.MonoScript))
                {
                    Debug.LogError($"What on earth did you find? path: {path}");
                }

                List<string> scriptLines = GetUpdatedScriptLines(path, fieldNames);

                Debug.Log($"{scriptLines}");

                SaveUpdateScript(path, scriptLines);
            }
        }

        private static List<string> GetUpdatedScriptLines(string scriptPath, List<string> fieldNames)
        {
            List<string> finalScriptLines = new List<string>();

            try
            {
                bool foundTmProUsings = false;
                bool foundClassOpening = false;
                bool foundClassDeclaration = false;
                bool insertedReplacerCode = false;

                using (var reader = new StreamReader(scriptPath))
                {
                    string line;
                    string classPattern = @"class\s\w+\s+:\s+MonoBehaviour";
                    string classOpenPattern = @"\{";
                    string indentationPattern = @"^\s+";
                    string tmProUsingsPattern = @"using\s+TMPro;";

                    //TODO omg what if there is a List of Text components somewhere in zula?
                    string fieldSearchPattern = @"(";
                    for (int i = 0; i < fieldNames.Count; i++)
                    {
                        fieldSearchPattern += $@"\sText\s+{fieldNames[i]};";
                        if (i < fieldNames.Count - 1) fieldSearchPattern += "|";
                    }
                    fieldSearchPattern += ")";

                    Debug.Log($"{fieldSearchPattern}");

                    Regex classRx = new Regex(classPattern);
                    Regex classOpenRx = new Regex(classOpenPattern);
                    Regex indentRx = new Regex(indentationPattern);
                    Regex tmProUsingsRx = new Regex(tmProUsingsPattern);
                    Regex fieldSearchRx = new Regex(fieldSearchPattern);

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (tmProUsingsRx.IsMatch(line))
                        {
                            foundTmProUsings = true;
                        }
                        else if (classRx.IsMatch(line))
                        {
                            foundClassDeclaration = true;
                        }
                        else if (foundClassDeclaration && classOpenRx.IsMatch(line))
                        {
                            foundClassOpening = true;
                        }
                        else if (!insertedReplacerCode && foundClassDeclaration && foundClassOpening)
                        {
                            Match indentation = indentRx.Match(line);

                            finalScriptLines.Add("#region Autogenerated UnityEngine.Text replacer code");
                            finalScriptLines.Add($"{indentation.Value}/* please don't edit */");
                            foreach (var fieldName in fieldNames)
                            {
                                IEnumerable<string> lines = GetAdapterTemplate(fieldName, indentation.Value);
                                finalScriptLines.AddRange(lines);
                            }
                            finalScriptLines.Add($"{indentation.Value}/* fin */");
                            finalScriptLines.Add("#endregion // Autogenerated UnityEngine.Text replacer code ");
                            finalScriptLines.Add(indentation.Value);

                            insertedReplacerCode = true;
                        }
                        
                        if (fieldSearchRx.IsMatch(line))
                        {
                            // We just want to skip the original field declaration line
                            continue;
                        }
                        else
                        {
                            finalScriptLines.Add(line);
                        }
                    }

                    if (foundTmProUsings == false)
                    {
                        finalScriptLines.Insert(0, "using TMPro;");
                    }
                }
            }
            catch (IOException e)
            {
                Debug.LogError("The file could not be read:");
                Debug.LogError(e.Message);
            }

            return finalScriptLines;
        }

        private static void SaveUpdateScript(string path, List<string> lines)
        {
            try
            {
                FileStream stream = new FileStream(path, FileMode.OpenOrCreate);
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    foreach (string line in lines)
                    {
                        writer.WriteLine(line);
                    }
                }
            }
            catch (IOException e)
            {
                Debug.LogError("The file could not be written:");
                Debug.LogError(e.Message);
            }
        }

        private static List<string> GetAdapterTemplate(string fileName, string indentation)
        {
            FileStream stream = new FileStream("Packages/com.mariaheineboombyte.fabulous-text-replacer/Editor/Templates/ShortAdapterTemplate.txt", FileMode.Open);

            string line;
            List<string> templateLines = new List<string>();
            string pattern = @"\{0\}";

            Regex rx = new Regex(@"\{0\}");

            using (var reader = new StreamReader(stream))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    line = Regex.Replace(line, pattern, fileName);
                    templateLines.Add($"{indentation}{line}");
                }
            }

            return templateLines;
        }

        #endregion // SCRIPT REPLACEMENT

        //
        // ─── TEXT COMPONENT REPLACEMENT ──────────────────────────────────
        //

        #region TEXT COMPONENT REPLACEMENT

        private void ReplaceTextComponent(UpdatedReference updatedReference)
        {
            TextMeshProUGUI newText;
            TextInformation textInfo = updatedReference.textInformation;

            // * Don't even think of performing below operations on previously saved prefabs loaded into the memory
            // * They are like lost souls that want to trap your innocent code
            // * Whatever you execute on them gets lost in a limbo and flushed down along the garbage collection
            // * If you want to edit a prefab, make sure you just loaded it and you work on a fresh, crunchy instance
            Component prefab = AssetDatabase.LoadAssetAtPath(updatedReference.prefabPath, typeof(Component)) as Component;

            // if (_reloadedComponents.ContainsKey(updatedReference.prefabPath))
            // {
            //     prefab = _reloadedComponents[updatedReference.prefabPath];
            // }
            // else
            // {
            //     prefab = AssetDatabase.LoadAssetAtPath(updatedReference.prefabPath, typeof(Component)) as Component;
            // }

            Text oldText = FabulousExtensions
                .GetGameObjectAtAddress(prefab.gameObject, updatedReference.TextAddress)
                .GetComponent<Text>();

            if (oldText != null)
            {
                UnityEngine.Object.DestroyImmediate(oldText, true);
                newText = textInfo.Parent.AddComponent<TextMeshProUGUI>();
            }
            else
            {
                newText = FabulousExtensions
                    .GetGameObjectAtAddress(prefab.gameObject, updatedReference.TextAddress)
                    .GetComponent<TextMeshProUGUI>();

                if (updatedReference.textInformation.Text.Contains("ALTERED"))
                {
                    Debug.Log($"0 {newText}, {newText.color} {newText}");
                }
            }

            newText.text = textInfo.Text;
            newText.alignment = textInfo.TMProAlignment;
            newText.font = fontAsset;
            newText.fontSize = (float)textInfo.FontSize;
            newText.color = textInfo.FontColor;
            newText.enableWordWrapping = true;

            PrefabUtility.RecordPrefabInstancePropertyModifications(newText);
            PrefabUtility.SavePrefabAsset(prefab.gameObject);
            // AssetDatabase.SaveAssets();

            // AssetDatabase.SaveAssets();
            // //* You may think the line below is not important but I've lost 4hours of work debugging why nested prefabs don't save their changes
            // AssetDatabase.ForceReserializeAssets(new string[] { updatedReference.prefabPath }, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
            // AssetDatabase.ImportAsset(updatedReference.prefabPath);
        }

        #endregion // TEXT COMPONENT REPLACEMENT
    }
}
