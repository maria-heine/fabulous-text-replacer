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
using System.Linq;
using System.Reflection;

namespace FabulousReplacer
{
    public class ScriptUpdater
    {
        private const string ADAPTER_PARENT_NAME = "{0}_TextAdaptersParent";

        UpdatedReferenceAddressBook _updatedReferenceAddressBook;
        Dictionary<Type, List<string>> _updatedMonoFields;
        Dictionary<Type, List<FieldInformation>> _fieldsToUpdateByType;
        IntegerField lowRange;
        IntegerField highRange;

        public ScriptUpdater(UpdatedReferenceAddressBook updatedReferenceAddressBook, Button updateScriptsButton, IntegerField lowRange, IntegerField highRange)
        {
            _updatedReferenceAddressBook = updatedReferenceAddressBook;
            _updatedMonoFields = new Dictionary<Type, List<string>>();
            this.lowRange = lowRange;
            this.highRange = highRange;

            updateScriptsButton.clicked += () =>
            {
                RunReplaceLogic();
            };
        }

        //? What about private Text fields?
        private void RunReplaceLogic()
        {
            int from = lowRange.value >= _updatedReferenceAddressBook.Count ? _updatedReferenceAddressBook.Count : lowRange.value;
            int to = highRange.value >= _updatedReferenceAddressBook.Count ? _updatedReferenceAddressBook.Count : highRange.value;

            for (int i = from; i < to; i++)
            {
                var references = _updatedReferenceAddressBook[i];

                foreach (ReplaceUnit reference in references)
                {
                    GatherFieldsToUpdate(reference);
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
        }

        //
        // ─── SCRIPT REPLACEMENT ──────────────────────────────────────────
        //

        #region SCRIPT REPLACEMENT 

        private void GatherFieldsToUpdate(ReplaceUnit reference)
        {
            if (reference.isReferenced)
            {
                // TODO this should use FieldInformation instead
                Type scriptType = reference.MonoType;
                string fieldName = reference.fieldName;

                if (_updatedMonoFields.ContainsKey(scriptType) && _updatedMonoFields[scriptType].Contains(fieldName))
                {
                    return;
                }
                else if (!_updatedMonoFields.ContainsKey(scriptType))
                {
                    _updatedMonoFields[scriptType] = new List<string>();
                }

                _updatedMonoFields[scriptType].Add(fieldName);
            }
        }

        private void MassReplaceFields()
        {
            foreach (var item in _updatedMonoFields)
            {
                Type monoType = item.Key;
                List<string> fieldNames = item.Value;

                string path = GetScriptFilePath(monoType);

                List<string> scriptLines = GetUpdatedScriptLines(path, monoType, fieldNames);

                SaveUpdateScript(path, scriptLines);
            }
        }

        /* 
        * This method can only work guaranteed the script files follow convention of being named the
        * same as the class they contain.
        TODO Something should be done about the possibility of partial classes
        TODO Apparently not much can be done:
        https://stackoverflow.com/questions/10960071/how-to-find-path-to-cs-file-by-its-type-in-c-sharp
        */ 
        private static string GetScriptFilePath(Type monoType)
        {
            string scriptFileName = monoType.Name;
            string[] assets = AssetDatabase.FindAssets($"{scriptFileName} t:MonoScript");

            string selectedAsset = null;

            if (assets.Length != 1)
            {
                foreach (string asset in assets)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(asset);

                    if (assetPath.Contains($"{scriptFileName}.cs"))
                    {
                        selectedAsset = asset;
                        Debug.Log($"From overlapping selections chose: {assetPath}");
                    }
                }
            }
            else
            {
                selectedAsset = assets[0];
            }

            var path = AssetDatabase.GUIDToAssetPath(selectedAsset);
            return path;
        }

        // TODO not finished
        private static Regex GetClassRx(FieldInformation fieldInformation)
        {
            string classPattern = null;
            FieldType fieldType = fieldInformation.FieldType;

            if (fieldType.HasFlag(FieldType.Nested))
            {
                Regex nestedClassRx = new Regex($@"\b\+\w+\b");
                string nestedClassName = nestedClassRx.Match(fieldInformation.FieldOwnerType.Name).Value;
                nestedClassName = nestedClassName.TrimStart('+');

                if (string.IsNullOrWhiteSpace(nestedClassName))
                {
                    Debug.LogError($"Failed to find nested class name for {fieldInformation.FieldOwnerType.Name}");
                }
                
                Debug.Log($"<color=yellow>{nestedClassName}</color>");
                classPattern = $@"\bclass\s+{nestedClassName}\b";
            }
            else
            {
                classPattern = $@"\bclass\s+{fieldInformation.FieldOwnerType.Name}\b";
            }

            return null;
        }

        // OuterClass+NestedClass

        private static List<string> GetUpdatedScriptLines(string scriptPath, Type monoType, List<string> fieldNames)
        {
            List<string> finalScriptLines = new List<string>();

            try
            {
                //bool foundTmProUsings = false;
                bool foundClassOpening = false;
                bool foundClassDeclaration = false;
                bool insertedReplacerCode = false;

                // TODO that check shouldn't be here
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
                    Regex classRx = new Regex(classPattern); //GetClassRx() //todo requires field infomration

                    string classOpenPattern = @"\{";
                    string indentationPattern = @"^\s+";
                    string tmProUsingsPattern = @"using\s+TMPro;";

                    //TODO omg what if there is a List of Text components somewhere in zula?
                    //* Hehe, there 'is' or rather 'are' and yes they are completely undalndled
                    //* Just the same with nested classes
                    string fieldSearchPattern = @"(";
                    for (int i = 0; i < fieldNames.Count; i++)
                    {
                        fieldSearchPattern += $@"\sText\s+{fieldNames[i]};";
                        if (i < fieldNames.Count - 1) fieldSearchPattern += "|";
                    }
                    fieldSearchPattern += ")";

                    Regex classOpenRx = new Regex(classOpenPattern);
                    Regex indentRx = new Regex(indentationPattern);
                    Regex tmProUsingsRx = new Regex(tmProUsingsPattern);
                    Regex fieldSearchRx = new Regex(fieldSearchPattern);

                    while ((line = reader.ReadLine()) != null)
                    {
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
                            finalScriptLines.Add($"{indentation.Value}#region Autogenerated UnityEngine.Text replacer code");
                            finalScriptLines.Add($"{indentation.Value}/* please don't edit or rename those fields */");
                            finalScriptLines.Add($"{indentation.Value}private const string ADAPTERS_PARENT_NAME = \"{String.Format(ADAPTER_PARENT_NAME, monoType.Name)}\";");
                            finalScriptLines.Add($"{indentation.Value}[Header(\"{monoType.Name} TextMeshPro Fields\")]");

                            foreach (var fieldName in fieldNames)
                            {
                                IEnumerable<string> lines = GetAdapterTemplate(fieldName, indentation.Value);
                                finalScriptLines.AddRange(lines);
                            }

                            finalScriptLines.Add($"{indentation.Value}/* fin */");
                            finalScriptLines.Add($"{indentation.Value}#endregion // Autogenerated UnityEngine.Text replacer code ");
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
    }
}
