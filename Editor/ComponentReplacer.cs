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
        List<TextRefernce> _textReferences;
        UpdatedReferenceAddressBook _updatedReferenceAddressBook;
        Dictionary<Type, List<string>> _updatedMonoFields;

        TMP_FontAsset fontAsset;

        public ComponentReplacer(UpdatedReferenceAddressBook updatedReferenceAddressBook, Button updateComponentsButton)
        {
            _updatedReferenceAddressBook = updatedReferenceAddressBook;
            _updatedMonoFields = new Dictionary<Type, List<string>>();

            updateComponentsButton.clicked += () =>
            {
                RunReplaceLogic();
            };

            fontAsset = AssetDatabase.LoadAssetAtPath("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset", typeof(TMP_FontAsset)) as TMP_FontAsset;
        }

        public void SetReplacerTextReferences(List<TextRefernce> textReferences)
        {
            _textReferences = textReferences;
        }

        // TODO What about private Text fields?

        // ! I am still missing the case of text components that dont have references at all
        private void RunReplaceLogic()
        {
            foreach (var kvp in _updatedReferenceAddressBook)
            {
                string prefabPath = kvp.Key;

                foreach (UpdatedReference reference in kvp.Value)
                {
                    // * Step: Update script
                    // TODO Alternate behaviour when the script was already modified for that field name
                    // TODO I can't
                    if (reference.isReferenced)
                    {
                        UpdateScript(reference.MonoType, reference.fieldName);
                    }
                    // AssetDatabase.

                    // * Step: Replace component
                    // ! What if that (oops! that cant be immediately removed here)
                    ReplaceTextComponent(reference);

                    AssetDatabase.SaveAssets();
                }
            }

            CompilationPipeline.compilationFinished += stuff =>
            {

            };

            CompilationPipeline.RequestScriptCompilation();
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

            //* This is easy in case of MonoBehaviours since their filename must match the calssname
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

        //
        // ─── TEXT COMPONENT REPLACEMENT ──────────────────────────────────
        //

        #region TEXT COMPONENT REPLACEMENT

        private void ReplaceTextComponent(UpdatedReference updatedReference)
        {
            if (updatedReference.originalText == null)
            {
                return;
            }
            else
            {
                Debug.Log($"Replacing text {updatedReference.originalText}, for: {updatedReference.originalText.transform.root.name}");
            }

            Debug.Log($"{updatedReference.originalText} size {updatedReference.textInformation.FontSize}");
            UnityEngine.Object.DestroyImmediate(updatedReference.originalText, true);

            TextInformation textInfo = updatedReference.textInformation;

            TextMeshProUGUI newText = textInfo.Parent.AddComponent<TextMeshProUGUI>();

            AssetDatabase.StartAssetEditing();
            newText.text = textInfo.Text;
            newText.alignment = textInfo.TMProAlignment;
            newText.font = fontAsset;
            newText.fontSize = (float)textInfo.FontSize;
            newText.color = textInfo.FontColor;
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        #endregion // TEXT COMPONENT REPLACEMENT
    }
}
