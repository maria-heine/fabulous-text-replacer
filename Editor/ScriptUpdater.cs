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

        [Obsolete] // TODO remove
        Dictionary<Type, List<string>> _updatedMonoFields;

        Dictionary<Type, List<FieldInformation>> _fieldsToUpdateByDefiningType;

        public ScriptUpdater(UpdatedReferenceAddressBook updatedReferenceAddressBook, Button updateScriptsButton)
        {
            _updatedReferenceAddressBook = updatedReferenceAddressBook;
            _updatedMonoFields = new Dictionary<Type, List<string>>();
            _fieldsToUpdateByDefiningType = new Dictionary<Type, List<FieldInformation>>();

            updateScriptsButton.clicked += () =>
            {
                RunReplaceLogic();
            };
        }

        //? What about private Text fields?
        private void RunReplaceLogic()
        {
            foreach (var reference in _updatedReferenceAddressBook)
            {
                List<ReplaceUnit> referenceGroup = reference.Value;

                foreach (ReplaceUnit replaceUnit in referenceGroup)
                {
                    GatherFieldsToUpdate(replaceUnit);
                }
            }

            //todo temporary
            MassReplaceFields();


            // try
            // {
            //     AssetDatabase.StartAssetEditing();
            //     MassReplaceFields();
            // }
            // catch (Exception ex)
            // {
            //     throw ex;
            // }
            // finally
            // {
            //     AssetDatabase.StopAssetEditing();
            // }

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
                Type definingType = reference.fieldInformation.FieldOwnerType;
                FieldInformation fieldInformation = reference.fieldInformation;

                //TODO blocked field information comparison, duplicates will happen
                // if (_fieldsToUpdateByType.ContainsKey(scriptType) && _fieldsToUpdateByType[scriptType].Contains())
                // {
                //     return;
                // }

                if (!_fieldsToUpdateByDefiningType.ContainsKey(definingType))
                {
                    _fieldsToUpdateByDefiningType[definingType] = new List<FieldInformation>();
                }

                _fieldsToUpdateByDefiningType[definingType].Add(fieldInformation);
            }
        }

        private void MassReplaceFields()
        {

            /*
            1. Direct fields
            Collect all of the fieldNames in given script and replace them in one sector

            2. Direct list
            Replace the list just once.
            Add references in next step

            3. Nested field
            //Gather all fields of a nested class
            Gather all different nested classes (usually oonly one)
            For each edit Text fields within

            4. List of nested fields
            Same as 3

            5. External class
            Gather all text fields to replace in external class
            */

            foreach (var item in _fieldsToUpdateByDefiningType)
            {
                Type fieldDefiningType = item.Key;
                List<FieldInformation> fieldInfos = item.Value;

                //* As we want to deal with script changes FieldType group one by one
                var fieldInfoGroups = fieldInfos
                    .GroupBy(fi => fi.FieldType, fi => fi);

                string path = null;
                List<string> scriptLines = null;

                foreach (var group in fieldInfoGroups)
                {
                    FieldType fieldType = group.Key;
                    Type editedScriptType;

                    switch (fieldType)
                    {
                        case FieldType.Nested:
                            editedScriptType = fieldInfos.First().FieldOwnerType;
                            Debug.Log($"{editedScriptType}");
                            break;
                        default:
                            editedScriptType = fieldDefiningType;
                            break;
                    }

                    path = GetScriptFilePath(editedScriptType);

                    //* Get a list of all FieldInformation within one FieldType, one fieldDefiningType 
                    //* with distinct actual fields that we want to update in code
                    FieldInformation[] distinctTextFieldInformations = group
                        .ToList()
                        .GroupBy(x => x.TextFieldName)
                        .Select(x => x.FirstOrDefault())
                        .ToArray();

                    if (scriptLines == null)
                    {
                        scriptLines = GetUpdatedScriptLines(path, editedScriptType, fieldType, distinctTextFieldInformations);
                    }
                    else
                    {
                        scriptLines = GetUpdatedScriptLines(scriptLines, fieldType, distinctTextFieldInformations);
                    }
                }

                SaveUpdateScript(path, scriptLines);
            }
        }

        const string REPLACER_REGION_TITLE = "Autogenerated UnityEngine.Text replacer code";
        const string REPLACER_END_STRING = "/* fin */";

        private static bool DoesScriptContain(string scriptPath, string regexExpression)
        {
            using (var reader = new StreamReader(scriptPath))
            {
                string content = reader.ReadToEnd();

                Regex replacerRx = new Regex(regexExpression);

                if (replacerRx.IsMatch(content))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private static List<string> ReadScriptLines(string scriptPath)
        {
            var scriptLines = new List<string>();

            using (var reader = new StreamReader(scriptPath))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    scriptLines.Add(line);
                }
            }

            return scriptLines;
        }

        private static List<string> GetTemplateLines(FieldType fieldsType, FieldInformation[] fieldInformations)
        {
            var templateLines = new List<string>();

            if (fieldsType == FieldType.Direct || fieldsType == FieldType.Nested || fieldsType == FieldType.External)
            {
                Debug.Log($"standard {templateLines.Count}");
                foreach (var fieldInformation in fieldInformations)
                {
                    IEnumerable<string> lines = GetAdapterTemplate(fieldInformation.TextFieldName);
                    templateLines.AddRange(lines);
                }
            }
            else if (fieldsType.HasFlag(FieldType.Arrayed | FieldType.Direct))
            {
                Debug.Log($"Arrayed direct");
                foreach (var fieldInformation in fieldInformations)
                {
                    string line = $"[SerializeField] TMProAdapter[] {fieldInformation.TextFieldName};";
                    templateLines.Add(line);
                }
            }
            else if (fieldsType.HasFlag(FieldType.Listed | FieldType.Direct))
            {
                Debug.Log($"Listed direct");
                foreach (var fieldInformation in fieldInformations)
                {
                    string line = $"[SerializeField] List<TMProAdapter> {fieldInformation.TextFieldName};";
                    templateLines.Add(line);
                }
            }
            else
            {
                Debug.LogError($"what? {fieldsType}");
            }

            return templateLines;
        }

        private static List<string> UpdateReplacerRegion(IEnumerable<string> currentScriptLines, FieldType fieldsType, FieldInformation[] fieldInformations)
        {
            Debug.Log($"Updating");

            List<string> finalLines = new List<string>();

            Regex replacerRegionEndRx = new Regex(REPLACER_END_STRING);
            Regex oldFieldSearchRx = GetOldFieldSearchPattern(fieldInformations);

            foreach (string line in currentScriptLines)
            {
                if (replacerRegionEndRx.IsMatch(line))
                {
                    finalLines.AddRange(GetTemplateLines(fieldsType, fieldInformations));
                }

                if (oldFieldSearchRx.IsMatch(line))
                {
                    //* We just want to skip the original field declaration line
                    continue;
                }
                else
                {
                    finalLines.Add(line);
                }
            }

            return finalLines;
        }

        private static List<string> InsertReplacerRegion(
            string scriptPath,
            Type scriptType,
            FieldInformation[] fieldInformations,
            IEnumerable<string> templateLines)
        {
            Debug.Log($"Inserting");

            List<string> finalScriptLines = new List<string>();

            Regex classRx = GetClass(fieldInformations.First());
            Regex classOpenRx = new Regex(@"\{");
            Regex indentRx = new Regex(@"^\s+");
            Regex oldFieldSearchRx = GetOldFieldSearchPattern(fieldInformations);

            bool foundClassOpening = false;
            bool foundClassDeclaration = false;
            bool insertedReplacerCode = false;

            using (var reader = new StreamReader(scriptPath))
            {
                string line;

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
                        finalScriptLines.Add($"{indentation.Value}#region {REPLACER_REGION_TITLE}");
                        finalScriptLines.Add($"{indentation.Value}private const string ADAPTERS_PARENT_NAME = \"{String.Format(ADAPTER_PARENT_NAME, scriptType.Name)}\";");
                        finalScriptLines.Add($"{indentation.Value}[Header(\"{scriptType.Name} TextMeshPro Fields\")]");

                        foreach (string templateLine in templateLines)
                        {
                            finalScriptLines.Add($"{indentation.Value}{templateLine}");
                        }

                        finalScriptLines.Add($"{indentation.Value}{REPLACER_END_STRING}");
                        finalScriptLines.Add($"{indentation.Value}#endregion // {REPLACER_REGION_TITLE}");
                        finalScriptLines.Add(indentation.Value);

                        insertedReplacerCode = true;
                    }

                    if (oldFieldSearchRx.IsMatch(line))
                    {
                        //* We just want to skip the original field declaration line
                        continue;
                    }
                    else
                    {
                        finalScriptLines.Add(line);
                    }
                }
            }

            return finalScriptLines;
        }

        private static List<string> GetUpdatedScriptLines(string scriptPath, Type editedScriptType, FieldType fieldsType, FieldInformation[] fieldInformations)
        {
            List<string> finalScriptLines = new List<string>();

            if (!DoesScriptContain(scriptPath, "using TMPro;"))
            {
                finalScriptLines.Add("using TMPro;");
            }

            if (!DoesScriptContain(scriptPath, "using UnityEngine;"))
            {
                finalScriptLines.Add("using UnityEngine;");
            }

            IEnumerable<string> templateLines = GetTemplateLines(fieldsType, fieldInformations);

            finalScriptLines.AddRange(InsertReplacerRegion(scriptPath, editedScriptType, fieldInformations, templateLines));

            return finalScriptLines;
        }

        private static List<string> GetUpdatedScriptLines(IEnumerable<string> currentScriptLines, FieldType fieldsType, FieldInformation[] fieldInformations)
        {
            return UpdateReplacerRegion(currentScriptLines, fieldsType, fieldInformations);
        }

        private static Regex GetOldFieldSearchPattern(FieldInformation[] fieldInformations)
        {
            FieldType fieldType = fieldInformations.First().FieldType;

            string singleFieldPattern = null;

            if (fieldType.HasFlag(FieldType.Listed | FieldType.Direct))
            {
                singleFieldPattern = @"\sList<Text>\s+{0}";
            }
            else if (fieldType.HasFlag(FieldType.Arrayed | FieldType.Direct))
            {
                singleFieldPattern = @"\sText\[\]\s+{0}";
            }
            else
            {
                singleFieldPattern = @"\sText\s+{0}";
            }

            string fieldSearchPattern = @"(";
            for (int i = 0; i < fieldInformations.Length; i++)
            {
                fieldSearchPattern += string.Format(singleFieldPattern, fieldInformations[i].TextFieldName);
                if (i < fieldInformations.Length - 1) fieldSearchPattern += "|";
            }
            fieldSearchPattern += ")";

            // Debug.Log($"{fieldSearchPattern}");

            return new Regex(fieldSearchPattern);
        }

        private static Regex GetClass(FieldInformation fieldInformation)
        {
            string className = fieldInformation.FieldDefiningType.Name;
            string expression = $@"\bclass\s+{className}\b";
            return new Regex(expression);
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

        [Obsolete]
        private static Regex GetClassRxPfffff(FieldInformation fieldInformation)
        {
            string classPattern = null;
            FieldType fieldType = fieldInformation.FieldType;

            if (fieldType.HasFlag(FieldType.Nested))
            {
                Regex nestedClassRx = new Regex($@"\b\+\w+\b");
                string nestedClassName = nestedClassRx.Match(fieldInformation.FieldDefiningType.Name).Value;
                nestedClassName = nestedClassName.TrimStart('+');

                if (string.IsNullOrWhiteSpace(nestedClassName))
                {
                    Debug.LogError($"Failed to find nested class name for {fieldInformation.FieldDefiningType.Name}");
                }

                Debug.Log($"<color=yellow>{nestedClassName}</color>");
                classPattern = $@"\bclass\s+{nestedClassName}\b";
            }
            else
            {
                classPattern = $@"\bclass\s+{fieldInformation.FieldDefiningType.Name}\b";
            }

            return null;
        }

        // OuterClass+NestedClass

        [Obsolete]
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
                                IEnumerable<string> lines = GetAdapterTemplate(fieldName);
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

        private static List<string> GetAdapterTemplate(string fileName)
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
                    templateLines.Add($"{line}");
                }
            }

            return templateLines;
        }

        #endregion // SCRIPT REPLACEMENT
    }
}
