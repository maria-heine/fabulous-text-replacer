using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using Button = UnityEngine.UIElements.Button;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;

namespace FabulousReplacer
{
    public class ScriptUpdater
    {
        private const string ADAPTER_PARENT_NAME = "{0}_TextAdaptersParent";

        UpdatedReferenceAddressBook _updatedReferenceAddressBook;
        Dictionary<Type, List<FieldInformation>> _fieldsToUpdateByFieldOwnerType;

        public ScriptUpdater(UpdatedReferenceAddressBook updatedReferenceAddressBook, Button updateScriptsButton)
        {
            _updatedReferenceAddressBook = updatedReferenceAddressBook;
            _fieldsToUpdateByFieldOwnerType = new Dictionary<Type, List<FieldInformation>>();

            updateScriptsButton.clicked += () =>
            {
                RunReplaceLogic();
            };
        }

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

            try
            {
                AssetDatabase.StartAssetEditing();
                MassReplaceFields();
            }
            catch (Exception ex)
            {
                throw ex;
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
                Type declaringType = reference.fieldInformation.FieldDeclaringType;
                FieldInformation fieldInformation = reference.fieldInformation;

                if (!_fieldsToUpdateByFieldOwnerType.ContainsKey(declaringType))
                {
                    _fieldsToUpdateByFieldOwnerType[declaringType] = new List<FieldInformation>();
                }

                _fieldsToUpdateByFieldOwnerType[declaringType].Add(fieldInformation);
            }
        }

        private void MassReplaceFields()
        {
            foreach (var item in _fieldsToUpdateByFieldOwnerType)
            {
                Type fieldDefiningType = item.Key;
                List<FieldInformation> fieldInfos = item.Value;

                //* As we want to deal with script changes FieldType group one by one
                var fieldInfoGroups = fieldInfos
                    .GroupBy(fi => fi.FieldType, fi => fi);

                Dictionary<string, List<string>> scriptLinesByPath = new Dictionary<string, List<string>>();

                foreach (var group in fieldInfoGroups)
                {
                    FieldType fieldType = group.Key;

                    //* Get a list of all FieldInformation within one FieldType, one fieldDefiningType 
                    //* with distinct actual fields that we want to update in code
                    FieldInformation[] distinctTextFieldInformations = group
                        .ToList()
                        .GroupBy(x => x.FieldName)
                        .Select(x => x.FirstOrDefault())
                        .ToArray();

                    string path = GetScriptFilePath(distinctTextFieldInformations.First());

                    if (!scriptLinesByPath.ContainsKey(path))
                    {
                        scriptLinesByPath.Add(path, new List<string>());
                        scriptLinesByPath[path] = GetUpdatedScriptLines(path, fieldDefiningType, fieldType, distinctTextFieldInformations);
                    }
                    else
                    {
                        scriptLinesByPath[path] = GetUpdatedScriptLines(scriptLinesByPath[path], fieldType, distinctTextFieldInformations);
                    }
                }

                if (scriptLinesByPath.Count > 1)
                {
                    Debug.Log($"<color=yellow>Actually had more than one path in case of {fieldDefiningType}</color>");
                }

                foreach (var kvp in scriptLinesByPath)
                {
                    SaveUpdateScript(kvp.Key, kvp.Value);
                }
            }
        }

        /* 
        * This method can only work guaranteed the script files follow convention of being named the
        * same as the class they contain.
        TODO Something should be done about the possibility of partial classes
        TODO Apparently not much can be done:
        https://stackoverflow.com/questions/10960071/how-to-find-path-to-cs-file-by-its-type-in-c-sharp
        */
        private string GetScriptFilePath(FieldInformation fieldInformation)
        {
            FieldType fieldType = fieldInformation.FieldType;
            string scriptFileName = null;

            //* Below is a bold assumbtion that healthy standards of defining a matching file name with the class it contains
            if (fieldType.HasFlag(FieldType.External))
            {
                scriptFileName = fieldInformation.FieldDeclaringType.Name;
            }
            else
            {
                scriptFileName = fieldInformation.FieldOwnerType.Name;
            }

            string selectedAsset = GetAssetGUIDByFileName(scriptFileName);

            if (selectedAsset == null)
            {
                //* thats in case an external class is defined within the same file as the type using it please y u do that :sob:

                foreach (string monoTypeName in _updatedReferenceAddressBook._allFoundMonoBehaviourTypeNames)
                {  
                    string otherMono = GetAssetGUIDByFileName(monoTypeName);
                    var otherMonoPath = AssetDatabase.GUIDToAssetPath(otherMono);
                    if (DoesScriptContain(otherMonoPath, $@"\bclass\s+{fieldInformation.FieldDeclaringType.Name}\b"))
                    {
                        Debug.Log($"Found {fieldInformation.FieldDeclaringType} in {monoTypeName} file");
                        return otherMonoPath;
                    }
                }
            }

            if (selectedAsset == null)
            {
                Debug.LogError($"A river of tears");
            }

            var path = AssetDatabase.GUIDToAssetPath(selectedAsset);

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"Pth is null for {scriptFileName} of owner: {fieldInformation.FieldOwnerType} declaring {fieldInformation.FieldDeclaringType} and asset: {selectedAsset}");
            }

            // Debug.Log($"{path}");

            return path;
        }

        private static string GetAssetGUIDByFileName(string scriptFileName)
        {
            string selectedAsset = null;

            string[] assets = AssetDatabase.FindAssets($"{scriptFileName} t:MonoScript");

            if (assets.Length != 1)
            {
                foreach (string asset in assets)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(asset);

                    if (assetPath.Contains($"{scriptFileName}.cs"))
                    {
                        selectedAsset = asset;
                        // Debug.Log($"From overlapping selections chose: {assetPath}");
                    }
                }
            }
            else
            {
                selectedAsset = assets[0];
            }

            return selectedAsset;
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

        private static List<string> UpdateReplacerRegion(IEnumerable<string> currentScriptLines, FieldType fieldsType, FieldInformation[] fieldInformations)
        {
            List<string> finalLines = new List<string>();

            string asd = "";
            foreach (var item in currentScriptLines)
            {
                asd += item + "\n";
            }
            // ANOTHER HACJK
            // because sometimes fields were written in duplicate
            FieldInformation[] filteredfi = fieldInformations.Where(fi => new Regex($@"{fi.FieldName}TMPro").IsMatch(asd) == false).ToArray();

            if (filteredfi.Length == 0) return currentScriptLines.ToList();

            Regex replacerRegionEndRx = new Regex(@"\bfin\b");
            //Regex oldFieldSearchRx = GetOldFieldSearchPattern(fieldInformations);
            Regex oldFieldSearchRx = GetOldFieldSearchPattern(filteredfi);
            Regex indentRx = new Regex(@"^\s+");

            foreach (string line in currentScriptLines)
            {
                if (replacerRegionEndRx.IsMatch(line))
                {
                    Match indentation = indentRx.Match(line);

                    //foreach (string templateLine in GetTemplateLines(fieldsType, fieldInformations))
                    foreach (string templateLine in GetTemplateLines(fieldsType, filteredfi))
                        {
                        finalLines.Add($"{indentation.Value}{templateLine}");
                    }
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

            //string asd = "";
            //foreach (var item in currentScriptLines)
            //{
            //    asd += item + "\n";
            //}
            //Debug.Log($"{asd}");

            return finalLines;
        }

        private static List<string> InsertReplacerRegion(
            string scriptPath,
            Type scriptType,
            FieldInformation[] fieldInformations,
            IEnumerable<string> templateLines)
        {
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

            // string asd = "";
            // foreach (var item in finalScriptLines)
            // {
            //     asd += item + "\n";
            // }
            // Debug.Log($"{asd}");

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

            if (fieldType.HasFlag(FieldType.Listed))
            {
                singleFieldPattern = @"\sList<Text>\s+{0}";
            }
            else if (fieldType.HasFlag(FieldType.Arrayed))
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
                fieldSearchPattern += string.Format(singleFieldPattern, fieldInformations[i].FieldName);
                if (i < fieldInformations.Length - 1) fieldSearchPattern += "|";
            }
            fieldSearchPattern += ")";

            // Debug.Log($"{fieldSearchPattern}");

            return new Regex(fieldSearchPattern);
        }

        private static Regex GetClass(FieldInformation fieldInformation)
        {
            string className = fieldInformation.FieldDeclaringType.Name;
            string expression = $@"\bclass\s+{className}\b";
            return new Regex(expression);
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

        private static List<string> GetTemplateLines(FieldType fieldsType, FieldInformation[] fieldInformations)
        {
            var templateLines = new List<string>();

            foreach (var fieldInformation in fieldInformations)
            {
                IEnumerable<string> lines = GetAdapterTemplate(fieldInformation.FieldName, fieldsType);
                templateLines.AddRange(lines);
            }

            return templateLines;
        }

        private const string STANDARD_TEMPLATE = "StandardAdapterTemplate";
        private const string LISTED_TEMPLATE = "ListedAdapterTemplate";
        private const string ARRAYED_TEMPLATE = "ArrayedAdapterTemplate";

        private static List<string> GetAdapterTemplate(string fileName, FieldType fieldType)
        {
            FileStream stream = null;

            string templatePath = null;

            if (fieldType == FieldType.Direct || fieldType == FieldType.Nested || fieldType == FieldType.External)
            {
                templatePath = $"Packages/com.mariaheineboombyte.fabulous-text-replacer/Editor/Templates/{STANDARD_TEMPLATE}.txt";
            }
            else if (fieldType.HasFlag(FieldType.Arrayed))
            {
                templatePath = $"Packages/com.mariaheineboombyte.fabulous-text-replacer/Editor/Templates/{ARRAYED_TEMPLATE}.txt";
            }
            else if (fieldType.HasFlag(FieldType.Listed))
            {
                templatePath = $"Packages/com.mariaheineboombyte.fabulous-text-replacer/Editor/Templates/{LISTED_TEMPLATE}.txt";
            }
            else
            {
                Debug.LogError($"Unhandled field type: {fieldType} for {fileName}, hacking around it.");
                templatePath = $"Packages/com.mariaheineboombyte.fabulous-text-replacer/Editor/Templates/{STANDARD_TEMPLATE}.txt";
            }

            stream = new FileStream(templatePath, FileMode.Open);

            if (stream == null)
            {
                Debug.LogError($"Failed to create template file streamfor path: {templatePath}");
            }

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
