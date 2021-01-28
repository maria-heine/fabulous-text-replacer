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
using System.Reflection;
using System.Threading.Tasks;

namespace FabulousReplacer
{
    public class ComponentReplacer
    {
        List<TextRefernce> _textReferences;
        Button _updateComponentsButton;
        UpdatedReferenceAddressBook _updatedReferenceAddressBook;
        Dictionary<Type,List<string>> _updatedMonoFields;

        public ComponentReplacer(UpdatedReferenceAddressBook updatedReferenceAddressBook, Button updateComponentsButton)
        {
            _updatedReferenceAddressBook = updatedReferenceAddressBook;
            _updateComponentsButton = updateComponentsButton;
            _updatedMonoFields = new Dictionary<Type, List<string>>();
            SetupButtons();
        }

        public void SetReplacerTextReferences(List<TextRefernce> textReferences)
        {
            _textReferences = textReferences;
        }

        private void SetupButtons()
        {
            _updateComponentsButton.clicked += () =>
            {
                // EditorCoroutineUtility.StartCoroutine(RunReplaceLogic(), editorWindow);
                RunReplaceLogic();
                // _updateReferencesButton.visible = true;
            };
        }

        // private IEnumerator RunReplaceLogic()
        private void RunReplaceLogic()
        {
            // 1. original text component shouldn't be updated before all its past references are saved somewhere
            // otherwise it will be impossible to know who else needs reference update
            TextRefernce testReference = _textReferences[0];


            foreach (var kvp in _updatedReferenceAddressBook)
            {
                string prefabPath = kvp.Key;

                foreach (UpdatedReference reference in kvp.Value)
                {
                    // This is easy in case of MonoBehaviours since their filename must match the calssname
                    // * Step: Update script
                    // TODO Alternate behaviour when the script was already modified for that field name
                    // TODO I can't
                    UpdateScript(reference.monoType, reference.fieldName);
                    // AssetDatabase.

                    // * Step: Replace component
                    // TODO What if that (oops! that cant be immediately removed here)
                    ReplaceTextComponent(reference, out TextMeshProUGUI newTMProComponent);
                }
            }

            AssetDatabase.SaveAssets();

            CompilationPipeline.compilationFinished += stuff =>
            {
                foreach (var kvp in _updatedReferenceAddressBook)
                {
                    Debug.Log(kvp.Key);

                    foreach (UpdatedReference reference in kvp.Value)
                    {
                        Debug.Log(reference.originalPrefab);
                        Debug.Log(reference.originalText);
                    }
                }
            };

            CompilationPipeline.RequestScriptCompilation();


            // foreach (MonoBehaviour localReference in testReference.localTextReferences)
            // {
            //     Type monoType = localReference.GetType();
            //     string scriptFileName = monoType.Name;
            //     string asmName = monoType.AssemblyQualifiedName;

            //     // This is easy in case of MonoBehaviours since their filename must match the calssname
            //     // * Step: Update script
            //     UpdateScript(testReference, localReference, out string newFieldName);
            //     // AssetDatabase.

            //     // * Step: Replace component
            //     ReplaceTextComponent(testReference, out TextMeshProUGUI newTMProComponent);
            //     testReference.updatedTMProText = newTMProComponent;

            //     AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            //     // * Step: Update new component references
            //     // TODO
            //     Debug.Log(EditorApplication.isCompiling);

            // }
        }

        private static void TestFields(Type monoType, string newFieldName, TextRefernce refernce)
        {
            Debug.Log("Testing fields");
            FieldInfo newTMProField = monoType.GetField("serializedTextFieldTMPro", ReferenceFinder.FIELD_SEARCH_FLAGS);
            FieldInfo testField = monoType.GetField("TestIntField", ReferenceFinder.FIELD_SEARCH_FLAGS);
            FieldInfo serializedTextField = monoType.GetField("serializedTextField", ReferenceFinder.FIELD_SEARCH_FLAGS);
            if (newTMProField == null) Debug.Log("this is bad");
            if (testField == null) Debug.Log("huh!");
            if (serializedTextField == null) Debug.Log("shouldnt be null!");
        }

        //
        // ─── SCRIPT REPLACEMENT ──────────────────────────────────────────
        //

        #region SCRIPT REPLACEMENT 

        private void UpdateScript(Type monoType, string fieldName)
        {
            if (_updatedMonoFields.ContainsKey(monoType) && _updatedMonoFields[monoType].Contains(fieldName))
            {
                return;
            }
            else
            {
                if (!_updatedMonoFields.ContainsKey(monoType))
                {
                    _updatedMonoFields[monoType] = new List<string>();
                }
                _updatedMonoFields[monoType].Add(fieldName);
            }

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

            List<string> lines = GetUpdatedScriptLines(path, fieldName);
            SaveUpdateScript(path, lines);

            fieldName = $"{fieldName}TMPro";
        }

        private static void UpdateScript(TextRefernce textReference, MonoBehaviour localReference, out string newFieldName)
        {
            Type scriptType = localReference.GetType();
            string scriptFileName = scriptType.Name;

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

            //TODO make tests if it correctly works with _ underscore names
            string fieldName = textReference.originalPrefabText.GetFieldNameForAComponent(owner: localReference);

            List<string> lines = GetUpdatedScriptLines(path, fieldName);
            SaveUpdateScript(path, lines);

            newFieldName = $"{fieldName}TMPro";

            // AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            // AssetDatabase.ImportAsset(path);

            // TODO temporarily disabled
            // AssetDatabase.Refresh();
        }

        private static List<string> GetUpdatedScriptLines(string path, string fieldName)
        {
            List<string> lines = new List<string>();

            try
            {
                using (var reader = new StreamReader(path))
                {
                    string line;
                    string textFieldPattern = $@"\s+Text\s+{fieldName}";
                    string indentationPattern = @"^\s+";

                    Regex textFieldRx = new Regex(textFieldPattern);
                    Regex indentRx = new Regex(indentationPattern);

                    // TODO hopefully in zula there are no places mixing tmpro and text yet
                    // ? otherwise no big deal, just add pre-check for existing TMPro using
                    lines.Add("using TMPro;");

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (textFieldRx.IsMatch(line))
                        {
                            Match indentation = indentRx.Match(line);
                            lines.AddRange(GetAdapterTemplate(fieldName, indentation.Value));
                        }
                        else
                        {
                            lines.Add(line);
                        }
                    }
                }
            }
            catch (IOException e)
            {
                Debug.LogError("The file could not be read:");
                Debug.LogError(e.Message);
            }

            return lines;
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
            FileStream stream = new FileStream("Packages/com.mariaheineboombyte.fabulous-text-replacer/Editor/Templates/AdapterTemplate.txt", FileMode.Open);

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

        private void ReplaceTextComponent(UpdatedReference updatedReference, out TextMeshProUGUI newTMProComponent)
        {
            Stack<int> textLocation = new Stack<int>(updatedReference.referencedTextAddress);
            string originalText = updatedReference.originalText.text;

            GameObject textParent = FabulousExtensions.GetGameObjectAtAddress(updatedReference.originalPrefab, textLocation);

            Debug.Log($"trying to get text field on prefab {updatedReference.originalPrefab} : {textParent.name}");
            if (textParent.TryGetComponent<Text>(out Text text))
            {
                UnityEngine.Object.DestroyImmediate(updatedReference.originalText, true);
                TextMeshProUGUI newText = textParent.AddComponent<TextMeshProUGUI>();
                newText.text = originalText;
                newTMProComponent = newText;
            }
            else
            {
                newTMProComponent = textParent.GetComponent<TextMeshProUGUI>();
            }
        }

        private void ReplaceTextComponent(TextRefernce textRefernce, out TextMeshProUGUI newTMProComponent)
        {
            GameObject textParent;
            textParent = textRefernce.originalPrefabText.gameObject;
            string originalText = textRefernce.originalPrefabText.text;
            UnityEngine.Object.DestroyImmediate(textRefernce.originalPrefabText, true);

            TextMeshProUGUI newText = textParent.AddComponent<TextMeshProUGUI>();
            newText.text = originalText;

            newTMProComponent = newText;
        }
    }
}
