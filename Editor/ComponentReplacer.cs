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

        public void SetReplacerTextReferences(List<TextRefernce> textReferences)
        {
            _textReferences = textReferences;
        }

        public Button GetReplacerButton(EditorWindow editorWindow)
        {
            var replacerButton = new Button(() =>
            {
                // EditorCoroutineUtility.StartCoroutine(RunReplaceLogic(), editorWindow);
                RunReplaceLogic();
            })
            { text = "Replace text components" };

            return replacerButton;
        }

        // private IEnumerator RunReplaceLogic()
        private async void RunReplaceLogic()
        {
            // 1. original text component shouldn't be updated before all its past references are saved somewhere
            // otherwise it will be impossible to know who else needs reference update
            TextRefernce testReference = _textReferences[0];

            // foreach (var kvp in testReference.textReferencesDictionary)
            // {
            //     Text textToReplace = kvp.Key;

            //     foreach (MonoBehaviour mono in kvp.Value)
            //     {

            //     }
            // }

            foreach (MonoBehaviour localReference in testReference.localTextReferences)
            {
                // if (testReference.isUpdatedUniqeMonobehaviourType[localReference.GetType()])
                // {
                //     return;
                // }

                Debug.Log(EditorApplication.isCompiling);

                Debug.Log(testReference.originalPrefabText);

                Type monoType = localReference.GetType();

                // This is easy in case of MonoBehaviours since their filename must match the calssname
                // * Step: Update script
                UpdateScript(testReference, localReference, out string newFieldName);
                // AssetDatabase.

                // * Step: Replace component
                ReplaceTextComponent(testReference, out TextMeshProUGUI newTMProComponent);

                // * Step: Update new component references
                // TODO
                Debug.Log(EditorApplication.isCompiling);

                // while (EditorApplication.isCompiling)
                // while (false)
                // {
                //     Debug.LogFormat("Time since startup: {0} s", Time.realtimeSinceStartup);
                //     yield return new EditorWaitForSeconds(0.1f);
                // }
                Debug.Log(testReference.originalPrefabText);
                
                CompilationPipeline.compilationStarted += ((somethign) => {
                    Debug.Log("Started");
                    Debug.Log(somethign);
                });
                CompilationPipeline.compilationFinished += ((somethign) => {
                    Debug.Log("Finished");
                    Debug.Log(EditorApplication.isCompiling);
                    Debug.Log(testReference.originalPrefabText.transform.root);
                    
                    TestFields(monoType, newFieldName, testReference);
                });
                CompilationPipeline.RequestScriptCompilation();
                // await Task.Delay(7000);k

                // yield return new EditorWaitForSeconds(5f);

                // Debug.Log(EditorApplication.isCompiling);
                 

                // newTMProField.SetValue(localReference, newTMProComponent);

                // * Step: Check if that script was present in one of instance references
                // foreach (var instanceRef in testReference.foreignTextReferencesDictionary)
                // {
                //     Text oldInstance = instanceRef.Key;
                //     if (oldInstance == null) Debug.LogError("its already null");
                //     else Debug.Log(oldInstance.transform.root.name);
                // }

                // testReference.isUpdatedUniqeMonobehaviourType[localReference.GetType()] = true;
            }

            // Debug.Log("foreign references");

            // foreach (var foreignReference in testReference.foreignTextReferencesDictionary)
            // {
            //     foreach (var mono in foreignReference.Value)
            //     {
            //         Debug.Log(mono.transform.root.name);
            //     }
            // }

            // ReplaceTextComponent(testReference);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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

            Debug.Log("wELL");
            string scriptFileName = monoType.Name;
            string[] assets = AssetDatabase.FindAssets($"{refernce.originalPrefabText.transform.root}");
            var path = AssetDatabase.GUIDToAssetPath(assets[0]);
            Debug.Log(path);
            UnityEngine.Object topAsset = AssetDatabase.LoadAssetAtPath(path, typeof(Component));
            Debug.Log(topAsset);
            
            // var fields = monoType.GetRuntimeFields();
            // foreach (var field in fields)
            // {
            //     Debug.Log(field.Name);
                
            // }
        }



        //
        // ─── SCRIPT REPLACEMENT ──────────────────────────────────────────
        //

        #region SCRIPT REPLACEMENT 

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
