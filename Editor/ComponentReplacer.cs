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
        UpdatedReferenceAddressBook _updatedReferenceAddressBook;
        Dictionary<Type, List<string>> _updatedMonoFields;
        IntegerField lowRange;
        IntegerField highRange;

        TMP_FontAsset fontAsset;

        public ComponentReplacer(UpdatedReferenceAddressBook updatedReferenceAddressBook, Button updateComponentsButton, IntegerField lowRange, IntegerField highRange)
        {
            _updatedReferenceAddressBook = updatedReferenceAddressBook;
            _updatedMonoFields = new Dictionary<Type, List<string>>();
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

        // TODO What about private Text fields?
        // ! I am still missing the case of text components that dont have references at all
        private void RunReplaceLogic()
        {
            int from = lowRange.value >= _updatedReferenceAddressBook.Count ? _updatedReferenceAddressBook.Count : lowRange.value;
            int to = highRange.value >= _updatedReferenceAddressBook.Count ? _updatedReferenceAddressBook.Count : highRange.value;

            for (int i = from; i < to; i++)
            {
                var references = _updatedReferenceAddressBook[i];

                foreach (UpdatedReference reference in references)
                {
                    GatherMono(reference);

                    // * Step: Replace component
                    ReplaceTextComponent(reference);
                }
            }

            //foreach (var kvp in _updatedReferenceAddressBook)
            //{
            //    string prefabPath = kvp.Key;

            //    foreach (UpdatedReference reference in kvp.Value)
            //    {
            //        GatherMono(reference);

            //        // * Step: Replace component
            //        ReplaceTextComponent(reference);
            //    }
            //}

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
                string[] assets = AssetDatabase.FindAssets($"{scriptFileName} t:MonoScript");

                if (assets.Length != 1)
                {
                    Debug.LogError($"Well, really we shouldn't find less or more than exactly one asset like that: {scriptFileName}");

                    foreach (string asset in assets)
                    {
                        Debug.LogError($"{AssetDatabase.GUIDToAssetPath(asset)}");
                    }

                    continue;
                }

                var path = AssetDatabase.GUIDToAssetPath(assets[0]);

                List<string> scriptLines = GetUpdatedScriptLines(path, monoType, fieldNames);

                SaveUpdateScript(path, scriptLines);
            }
        }

        private static List<string> GetUpdatedScriptLines(string scriptPath, Type monoType, List<string> fieldNames)
        {
            List<string> finalScriptLines = new List<string>();

            try
            {
                //bool foundTmProUsings = false;
                bool foundClassOpening = false;
                bool foundClassDeclaration = false;
                bool insertedReplacerCode = false;

                if (scriptPath.Contains(".cs") == false)
                {
                    Debug.LogError($"path does not point to a script file: {scriptPath}");
                }

                //TODO add check if script already imports TMPro
                finalScriptLines.Add("using TMPro;");

                using (var reader = new StreamReader(scriptPath))
                {
                    string line;
                    string classPattern = $@"\bclass\s+{monoType.Name}\b";
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

                    Regex classRx = new Regex(classPattern);
                    Regex classOpenRx = new Regex(classOpenPattern);
                    Regex indentRx = new Regex(indentationPattern);
                    Regex tmProUsingsRx = new Regex(tmProUsingsPattern);
                    Regex fieldSearchRx = new Regex(fieldSearchPattern);

                    while ((line = reader.ReadLine()) != null)
                    {
                        //if (tmProUsingsRx.IsMatch(line))
                        //{
                        //    foundTmProUsings = true;
                        //}
                        if (classRx.IsMatch(line))
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

                            finalScriptLines.Add(indentation.Value);
                            finalScriptLines.Add("#region Autogenerated UnityEngine.Text replacer code");
                            finalScriptLines.Add($"{indentation.Value}/* please don't edit */");
                            finalScriptLines.Add("[Header(\"TextmeshPro Fields\")]");

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

                    ////TODO How the hell did that get added into a prefab file
                    //if (foundTmProUsings == false)
                    //{
                    //    finalScriptLines.Insert(0, "using TMPro;");
                    //}
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
            }

            newText.text = textInfo.Text;
            newText.alignment = textInfo.TMProAlignment;
            newText.font = fontAsset;
            newText.fontSize = (float)textInfo.FontSize;
            newText.color = textInfo.FontColor;
            newText.enableWordWrapping = true;

            // TODO oki that is a small hack
            // using original font size as max size and always enabling auto sizing
            newText.fontSizeMax = textInfo.FontSize;
            //newText.enableAutoSizing = textInfo.AutoSize;
            newText.enableAutoSizing = true;

            newText.fontSizeMin = textInfo.MinSize;
            newText.richText = textInfo.IsRichText;
            newText.characterSpacing = -1.1f;

            PrefabUtility.RecordPrefabInstancePropertyModifications(newText);
            PrefabUtility.SavePrefabAsset(prefab.gameObject);

            // AssetDatabase.SaveAssets();
            // //* You may think the line below is not important but I've lost 4hours of work debugging why nested prefabs don't save their changes
            // AssetDatabase.ForceReserializeAssets(new string[] { updatedReference.prefabPath }, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
            // AssetDatabase.ImportAsset(updatedReference.prefabPath);
        }

        #endregion // TEXT COMPONENT REPLACEMENT
    }
}
