using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using static FabulousReplacer.FabulousExtensions;

namespace FabulousReplacer
{
    public partial class FabulousTextComponentReplacer : EditorWindow
    {
        const string SEARCH_DIRECTORY = "Assets/RemoteAssets";
        /*
        ! Note that "Assets/Original/Prefabs"
        ! is entirely different than "Assets/Original/Prefabs/"
        ! find the difference
        */
        const string PREFABS_ORIGINAL_LOCATION = "Assets/Original/Prefabs";
        const string PREFABS_COPY_LOCATION = "Assets/Copy/Prefabs";

        TextField textField;
        Box boxDisplayer;

        List<GameObject> _loadedPrefabs;
        Dictionary<GameObject, List<GameObject>> _crossPrefabReferences;
        Dictionary<GameObject, List<MonoBehaviour>> _customMonobehavioursByPrefab;
        Dictionary<GameObject, List<GameObject>> _nestedPrefabs;
        Dictionary<Type, List<GameObject>> _scriptReferences;
        Dictionary<GameObject, List<Type>> _scriptsByPrefab;
        Dictionary<string, string> _scriptCopies;
        ReplaceCounter _replaceCounter;
        bool isBackupMade;

        Action UpgradeProgressBar;

        private class ReplaceCounter
        {
            public int totalTextComponentCount = 0;
            public int totalTextComponentReferencesCount = 0;
            public int updatedTextComponentCount = 0;
            public int updatedTextComponentReferencesCount = 0;
        }

        #region  EDITOR WINDOW STARTUP

        // Note: The characters _%#T at the end of the MenuItem string lets us add a shortcut to open the window, which is here CTRL + SHIFT + T.
        [MenuItem("Window/Zula Mobile/Fabulous Text Component Replacer _%#T")]
        public static void ShowWindow()
        {
            // Opens the window, otherwise focuses it if it’s already open.
            var window = GetWindow<FabulousTextComponentReplacer>();

            // Adds a title to the window.
            window.titleContent = new GUIContent("Fabulous Text Component Replacer");

            // Sets a minimum size to the window.
            window.minSize = new Vector2(250, 50);
        }

        private void DisplayInBox(VisualElement toDisplay)
        {
            boxDisplayer.Clear();
            var scrollview = new ScrollView();
            scrollview.Add(toDisplay);
            boxDisplayer.Add(scrollview);
        }

        private void OnEnable()
        {
            // Reference to the root of the window
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;
            var menuBox = new Box();
            menuBox.style.alignItems = Align.Stretch;
            menuBox.style.width = new StyleLength(250f);
            var dataBox = new Box();
            root.Add(menuBox);
            root.Add(dataBox);
            boxDisplayer = new Box();
            dataBox.Add(boxDisplayer);

            DrawInitializeSection(menuBox);
            DrawTestTextReplacer(menuBox);
            DrawLoggingButtons(menuBox);
            DrawProgressStatus(menuBox);
        }

        private void DrawProgressStatus(Box menuBox)
        {
            var container = new Box();
            menuBox.Add(container);

            var label = new Label() { text = "Progress status" };
            container.Add(label);

            var totalTextCount = new TextElement();
            container.Add(totalTextCount);

            var missedTextComponentCount = new TextElement();
            missedTextComponentCount.style.color = Color.red;
            container.Add(missedTextComponentCount);

            var totalTextReference = new TextElement();
            totalTextReference.style.color = Color.black;
            container.Add(totalTextReference);

            var missedTextReferences = new TextElement();
            missedTextReferences.style.color = Color.red;
            container.Add(missedTextReferences);

            var progressBar = new ProgressBar();
            progressBar.style.height = new StyleLength(15f);
            progressBar.title = "Progress";
            container.Add(progressBar);

            UpgradeProgressBar += () =>
            {
                totalTextCount.text = $"Total Text Component Count: {_replaceCounter.totalTextComponentCount.ToString()}";
                // TODO Add escond progress bar
                totalTextReference.text = $"Total Text Reference Count: {_replaceCounter.totalTextComponentReferencesCount.ToString()}";
                missedTextReferences.text = $"Missed Component Count: {(_replaceCounter.totalTextComponentReferencesCount - _replaceCounter.updatedTextComponentReferencesCount).ToString()}";
                progressBar.value = ((float)_replaceCounter.updatedTextComponentReferencesCount / _replaceCounter.totalTextComponentReferencesCount) * 100f;
                if (progressBar.value > 90f)
                {
                    progressBar.title = $"{progressBar.value}% references coverage";
                }
            };
        }

        private void DrawTestTextReplacer(VisualElement root)
        {
            var container = new Box();
            root.Add(container);

            string testAsset = "Assets/RemoteAssets/UI/ActionPopups/FriendActionPopupView.prefab";

            var label = new Label() { text = "Test Replacer" };
            container.Add(label);

            var domagicbutton = new Button(() =>
            {
                RectTransform loadedAsset = (RectTransform)AssetDatabase.LoadAssetAtPath(testAsset, typeof(RectTransform));
                var text = loadedAsset.GetComponentInChildren<Text>();
                loadedAsset.gameObject.AddComponent<Dropdown>();
                AssetDatabase.SaveAssets();
            })
            { text = "Do magic button" };

            var analyseprefabbuttin = new Button(() =>
            {
                List<string> analysisResultsParts = new List<string>();

                int currentDepth = 0;
                var msb = new MultilineStringBuilder("1 - Prefab analysis");

                foreach (var prefab in _loadedPrefabs)
                {
                    if (msb.Length > 5000)
                    {
                        analysisResultsParts.Add(msb.ToString());
                        msb = new MultilineStringBuilder($"{analysisResultsParts.Count + 1} - Prefab analysis");
                    }
                    AnalyzePrefab(prefab, _loadedPrefabs, msb, ref currentDepth);
                    currentDepth++;
                }

                UpgradeProgressBar.Invoke();

                try
                {
                    VisualElement el = new VisualElement();
                    foreach (var analysisSegment in analysisResultsParts)
                    {
                        el.Add(GetTextElement(analysisSegment));
                    }
                    DisplayInBox(el);
                }
                catch (Exception ex)
                {
                    Debug.Log(ex.Message);
                }
            })
            { text = "Analyse prefabs" };

            var clearBackupButton = new Button(() =>
            {
                ClearAndRevertBackup();
            })
            { text = "Clear backup" };
            container.Add(clearBackupButton);
            container.Add(analyseprefabbuttin);
        }

        #endregion //  EDITOR WINDOW STARTUP

        //
        // ─── INITIALIZATION ─────────────────────────────────────────────────────────────
        //
        #region Initialization

        private void DrawInitializeSection(VisualElement root)
        {
            var label = new Label() { text = "Initialization" };
            root.Add(label);

            IntegerField depthSearchIntField = new IntegerField("Prefab Search Depth");
            depthSearchIntField.value = -1;
            root.Add(depthSearchIntField);

            ToolbarToggle copiesAndBackupToggle = new ToolbarToggle() { text = "Prepare Copies And Backup", label = "Backup?" };
            copiesAndBackupToggle.value = true;
            root.Add(copiesAndBackupToggle);

            Button initializeButton = new Button(() =>
            {
                if (copiesAndBackupToggle.value)
                {
                    PrepareCopiesAndBackup();
                }

                //* Where to search for prefabs (depending on whether we make a backup or not)
                // ! Prefab backup abandoned
                LoadAllPrefabs();
                FindCrossPrefabReferences(depthSearchIntField.value);
                FindScriptReferences(depthSearchIntField.value);
                UpgradeProgressBar.Invoke();
            })
            { text = "Initialize" };

            root.Add(initializeButton);
        }

        private void PrepareCopiesAndBackup()
        {
            ClearAndRevertBackup();

            // ! oki, operating on copied prefabs would require A LOT of work
            // ! because copied prefabs keep their old references inside their nested prefabs
            // ! each of thoose nested references would have to be overriden manualy
            // ! lots of work
            // string path = AssetDatabase.GUIDToAssetPath( PREFABS_ORIGINAL_LOCATION );
            // string[] assetsToCopy = AssetDatabase.FindAssets("", new[] { "Assets/Original/Prefabs/" });
            // Debug.Log(assetsToCopy.Length);

            // foreach (var asset in assetsToCopy)
            // {
            //     AssetDatabase.CopyAsset(asset, PREFABS_COPY_LOCATION);
            // }

            // you cant copyyyy like that baby, references get fukked
            // FileUtil.CopyFileOrDirectory(PREFABS_ORIGINAL_LOCATION, PREFABS_COPY_LOCATION);
            // AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            _scriptCopies = new Dictionary<string, string>();

            //* Handle script backups
            string scriptsRoot = "Assets/Original/Scripts";
            string[] files = Directory.GetFiles(scriptsRoot);

            string[] directories = Directory.GetDirectories(scriptsRoot, "*", SearchOption.AllDirectories);

            foreach (var dir in directories)
            {
                files.Concat(Directory.GetFiles(dir));
            }
            // Debug.Log(files.Length);

            foreach (var file in files)
            {
                if (file.Contains(".cs") && !file.Contains(".meta"))
                {
                    try
                    {
                        using (var sr = new StreamReader(file))
                        {
                            string content = sr.ReadToEnd();
                            // Debug.Log(content);
                            _scriptCopies.Add(file, content);
                        }
                    }
                    catch (IOException e)
                    {
                        Debug.LogError("The file could not be read:");
                        Debug.LogError(e.Message);
                    }
                }
            }

            isBackupMade = true;
        }

        private void ClearAndRevertBackup()
        {
            isBackupMade = false;

            if (Directory.Exists(PREFABS_COPY_LOCATION))
            {
                //! THIS IS SO UNGODLY ANNOYING, HARDCODING THIS SHTI, JUST DONT USE IT ON DEV
                //? Oki got this, u must remove a .meta file of a directory before removing that dir
                //? Then FileUtil works as expected
                FileUtil.DeleteFileOrDirectory("Assets/Copy/Prefabs.meta"); //! because unity wont let u delete an empty folder while leaving its meta file
                FileUtil.DeleteFileOrDirectory(PREFABS_COPY_LOCATION);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
        }

        private void LoadAllPrefabs()
        {
            _loadedPrefabs = new List<GameObject>();
            _replaceCounter = new ReplaceCounter();

            /* 
            * Note: This will count both directories and script files as assets
            */

            string[] assets = AssetDatabase.FindAssets("t:Object", new[] { SEARCH_DIRECTORY });

            Debug.Log($"Found {assets.Length} assets.");

            for (int i = 0; i < assets.Length; i++)
            {
                string objectsPath = AssetDatabase.GUIDToAssetPath(assets[i]);

                UnityEngine.Object topAsset = AssetDatabase.LoadAssetAtPath(objectsPath, typeof(Component));

                try
                {
                    // Since only prefabs in assets will have a component as it's root
                    if (topAsset is Component c)
                    {
                        _loadedPrefabs.Add(c.gameObject);

                        // ! Counting all found text components
                        _replaceCounter.totalTextComponentCount += c.gameObject.GetComponentsInChildren<Text>(includeInactive: true).Count();
                    }
                }
                catch (Exception)
                {
                    /* Because I don't care about those two unnameable ghostly objects that exist and yet they don't */
                    Debug.Log($"Prefabl searcher exception for path: {objectsPath}");
                }
            }
        }

        private void FindCrossPrefabReferences(int searchDepth = -1)
        {
            int currentDepth = 0;

            _crossPrefabReferences = new Dictionary<GameObject, List<GameObject>>();
            _nestedPrefabs = new Dictionary<GameObject, List<GameObject>>();

            foreach (var rootPrefab in _loadedPrefabs)
            {
                if (searchDepth == -1 || currentDepth < searchDepth) currentDepth++;
                else return;

                List<GameObject> foundNestedPrefabs = rootPrefab.CheckHierarchyForNestedPrefabs();

                if (foundNestedPrefabs.Count() > 0)
                {
                    _nestedPrefabs.Add(rootPrefab, foundNestedPrefabs);

                    bool foundErrors = false;
                    MultilineStringBuilder msb = new MultilineStringBuilder($"Cross Prefab Reference serach, load failures f or <<{rootPrefab.name}>>");

                    foreach (var nestedPrefab in foundNestedPrefabs)
                    {
                        //! This has to be done since nested prefabs are separate instances
                        // Than those we loaded with LoadAllPrefabs

                        var loadedInstance = _loadedPrefabs.FindInstanceOfTheSamePrefab(nestedPrefab);

                        if (loadedInstance == null)
                        {
                            foundErrors = true;
                            msb.AddLine($"Failed to find {nestedPrefab.name} in loadedPrefabs");
                            continue;
                        }

                        if (!_crossPrefabReferences.ContainsKey(loadedInstance))
                        {
                            _crossPrefabReferences[loadedInstance] = new List<GameObject>();
                        }

                        _crossPrefabReferences[loadedInstance].Add(rootPrefab);
                    }

                    if (foundErrors) Debug.LogError(msb.ToString());
                }
            }
        }

        private void FindScriptReferences(int searchDepth = -1)
        {
            int currentDepth = 0;

            _scriptReferences = new Dictionary<Type, List<GameObject>>();
            _scriptsByPrefab = new Dictionary<GameObject, List<Type>>();
            _customMonobehavioursByPrefab = new Dictionary<GameObject, List<MonoBehaviour>>();

            foreach (var prefab in _loadedPrefabs)
            {
                if (searchDepth == -1 || currentDepth < searchDepth) currentDepth++;
                else return;

                prefab.FindScriptsInHierarchy(out List<MonoBehaviour> foundScripts);
                _customMonobehavioursByPrefab[prefab] = foundScripts;

                _scriptsByPrefab[prefab] = foundScripts
                    .Select((instance) => instance.GetType())
                    .Distinct()
                    .ToList();

                if (foundScripts.Count > 0)
                {
                    foreach (var mono in foundScripts)
                    {
                        Type monoType = mono.GetType();

                        if (!_scriptReferences.ContainsKey(monoType))
                        {
                            _scriptReferences[monoType] = new List<GameObject>();
                        }

                        _replaceCounter.totalTextComponentReferencesCount += mono.GetComponentReferenceCount<Text>();

                        // TODO Remeber to later find all instances of such script in a mono since there may be multiple
                        if (_scriptReferences[monoType].Contains(prefab)) continue;

                        _scriptReferences[monoType].Add(prefab);
                    }
                }
            }
        }


        #endregion // Initialization

        //
        // ─── LOGGING ────────────────────────────────────────────────────────────────────
        //

        #region LOGGING

        private void DrawLoggingButtons(VisualElement root)
        {
            var label = new Label() { text = "Logging" };
            var box = new Box();
            box.style.height = StyleKeyword.Auto;
            box.style.overflow = Overflow.Visible;
            root.Add(label);
            root.Add(box);

            IntegerField loggingDepthField = new IntegerField("Logging Depth");
            loggingDepthField.value = 30;
            box.Add(loggingDepthField);

            var logCrossReferences = new Button(() =>
            {
                var msb = new MultilineStringBuilder("Log Cross References");

                LogCrossReferences(msb, loggingDepthField.value);

                DisplayInBox(GetTextElement(msb.ToString()));
            })
            { text = "Log Cross References" };
            box.Add(logCrossReferences);

            var logScriptReferencesButton = new Button(() =>
            {
                var msb = new MultilineStringBuilder("Log Script References");

                LogScriptReferences(msb, loggingDepthField.value);

                DisplayInBox(GetTextElement(msb.ToString()));
            })
            { text = "Log Script References" };
            box.Add(logScriptReferencesButton);
        }

        private void LogCrossReferences(MultilineStringBuilder msb, int logDepth = 30)
        {
            int depth = 0;

            Debug.Log(_crossPrefabReferences.Count);

            foreach (var c in _crossPrefabReferences)
            {
                msb.AddLine(new[] { "key: ", c.Key.name, " has reference count: ", c.Value.Count.ToString() });

                foreach (var reference in c.Value)
                {
                    msb.AddLine(new[] { "---> ", reference.name });
                }

                if (depth > logDepth) return;
                depth++;
            }
        }

        private void LogScriptReferences(MultilineStringBuilder msb, int logDepth = 30)
        {
            int depth = 0;

            foreach (var c in _scriptReferences)
            {
                msb.AddLine(new[] { "mono: ", c.Key.Name, " has reference count: ", c.Value.Count.ToString() });

                foreach (var reference in c.Value)
                {
                    msb.AddLine(new[] { "---> ", reference.name });
                }

                if (depth > logDepth) return;
                depth++;
            }
        }

        #endregion // LOGGING

        #region ANALYSIS

        /// <summary>
        /// 
        /// </summary>
        /// <param name="originalPrefab"></param>
        /// <param name="originalText"></param>
        /// <returns>Returns a dictionery of prefabs referencing an instance of the originalPrefab with a list of it's specific Text components being referenced</returns>
        private Dictionary<GameObject, List<Text>> GetAllTextInstances(GameObject originalPrefab, Text originalText)
        {
            Dictionary<GameObject, List<Text>> prefabTextInstances = new Dictionary<GameObject, List<Text>>();

            if (_crossPrefabReferences.ContainsKey(originalPrefab))
            {
                foreach (GameObject thisPrefabReferncer in _crossPrefabReferences[originalPrefab])
                {
                    foreach (GameObject referencerNestedPrefab in _nestedPrefabs[thisPrefabReferncer])
                    {
                        if (AreInstancesOfTheSamePrefab(referencerNestedPrefab, originalPrefab))
                        {
                            try
                            {
                                Text textInstance = GetSameComponentForDuplicate<Text>(originalPrefab, originalText, referencerNestedPrefab);

                                if (textInstance != null)
                                {
                                    if (!prefabTextInstances.ContainsKey(thisPrefabReferncer))
                                    {
                                        prefabTextInstances[thisPrefabReferncer] = new List<Text>();
                                    }

                                    prefabTextInstances[thisPrefabReferncer].Add(textInstance);
                                }
                                else
                                {
                                    Debug.LogError("That really shouldn't be null");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"Failed to get duplicate of a {originalText.name} component of an original <<{originalPrefab.name}>> gameobject and it's duplicate <<{referencerNestedPrefab.name}>> in <<{thisPrefabReferncer}>>");
                            }
                        }
                    }
                }
            }

            return prefabTextInstances;
        }

        private void AnalyzePrefab(GameObject prefab, List<GameObject> foundPrefabs, MultilineStringBuilder msb, ref int currentDepth)
        {
            currentDepth++;

            List<string> nestedPrefabs = new List<string>();

            Dictionary<Text, List<Component>> localTextReferencesDictionary = new Dictionary<Text, List<Component>>();
            Dictionary<Text, List<Component>> foreignTextReferencesDictionary = new Dictionary<Text, List<Component>>();

            // * Step one
            if (!prefab.TryGetComponentsInChildren<Text>(out List<Text> localTextComponents, skipNestedPrefabs: true))
            {
                //msb.AddLine($"{prefab.name} has no text components");
                //msb.AddSeparator();
                return;
            }

            // todo I am skipping case when a prefab references a nested prefab of its nested prefab fukk that

            List<TextRefernce> textRefernces = new List<TextRefernce>(localTextComponents.Count);

            foreach (Text text in localTextComponents)
            {
                TextRefernce textRef = new TextRefernce(text);
                textRefernces.Add(textRef);

                //! 1. Internal text component references
                //* Considering simplest case when text component is only referenced within a single prefabvb
                if (prefab.TryExtractTextReferences(text, _customMonobehavioursByPrefab[prefab], out List<Component> textReferences))
                {
                    localTextReferencesDictionary[text] = textReferences;
                    textRef.SetLocalTextReferences(textReferences);
                    _replaceCounter.updatedTextComponentReferencesCount += textReferences.Count;
                }

                //! 2. Text components referenced by nested prefabs
                //! 3. Text components of prefabs that are reference by the other nested prefabs
                //! X???. Text components of prefabs that are referenced by parent of a parent OR by a nested prefab of a parent of a parent OOF
                //! 4. Foreign text component references
                //! References to text components in 
                // Get all instances of a Text component along with the parentPrefab that is holding them
                // It is a list of Text components because parentPrefab may hold multiple references to that Text Component
                // TODO 1. making sure that GetAllTextInstances really gets ALL of them
                foreach (var kvp in GetAllTextInstances(prefab, text))
                {
                    GameObject parentPrefab = kvp.Key;
                    List<Text> textComponentInstances = kvp.Value;

                    foreach (Text textInstance in textComponentInstances)
                    {
                        // TODO 2. making sure that
                        bool foundMonobehaviourReferences = parentPrefab
                            .TryExtractTextReferences(
                                text: textInstance,
                                monoBehaviourToCheck: _customMonobehavioursByPrefab[parentPrefab],
                                textReferences: out List<Component> textInstanceReferences);

                        if (foundMonobehaviourReferences)
                        {
                            if (!foreignTextReferencesDictionary.ContainsKey(textInstance))
                            {
                                foreignTextReferencesDictionary.Add(textInstance, textInstanceReferences);
                            }
                            else
                            {
                                foreignTextReferencesDictionary[textInstance].AddRange(textInstanceReferences);
                            }

                            textRef.AddForeignTextReference(textInstance, textInstanceReferences);
                            _replaceCounter.updatedTextComponentReferencesCount += textInstanceReferences.Count;
                        }
                        else
                        {
                            // TODO  Just add a reference to a found text instance that we didn't find other scipts referencing it
                            textRef.AddTextInstance(textInstance);
                        }
                    }
                }
            }

            ////todo Test overwwrite some field
            //var tempmonolist = new List<MonoBehaviour>();
            //if (prefab.TryGetScripts(tempmonolist))
            //{
            //    foreach (var mono in tempmonolist)
            //    {
            //        if (mono.GetType().Name.Contains("PublicField"))
            //        {
            //            // Debug.Log($"Found a public field scriptin {prefab.name}");
            //            mono.GetType().GetField("SomeOtherString").SetValue(mono, "yay!");
            //            AssetDatabase.SaveAssets();
            //        }
            //    }
            //}

            msb.AddLine($"Analysis of {prefab.name}");
            if (nestedPrefabs.Count > 0)
            {
                msb.AddLine($"Has nested prefabs:");
                foreach (var n in nestedPrefabs)
                {
                    msb.AddLine($"---> {n}");
                }
            }
            if (localTextComponents.Count > 0)
            {
                msb.AddLine($"Has text components:");
                foreach (var n in localTextComponents)
                {
                    msb.AddLine($"---> {n.gameObject}");
                }
            }
            if (_scriptsByPrefab[prefab].Count > 0)
            {
                msb.AddLine($"Has custom monobehaviours:");
                foreach (Type monoType in _scriptsByPrefab[prefab])
                {
                    msb.AddLine($"---> {monoType.Name}");
                }
            }
            if (localTextReferencesDictionary.Count > 0)
            {
                msb.AddLine($"Has internal text references:");
                foreach (var kvp in localTextReferencesDictionary)
                {
                    msb.AddLine($"---> {kvp.Key}");
                    msb.AddLine("---> Is referenced at:");
                    foreach (var fukkkk in kvp.Value)
                    {
                        msb.AddLine($"------> {fukkkk}");
                    }
                }
            }
            if (_crossPrefabReferences.ContainsKey(prefab))
            {
                msb.AddLine($"Is referenced by other prefabs:");
                foreach (GameObject referencer in _crossPrefabReferences[prefab])
                {
                    msb.AddLine($"---> by: {referencer}");
                }
            }
            if (foreignTextReferencesDictionary.Count > 0)
            {
                msb.AddLine($"(!!!) Has foreign text references:");
                foreach (var kvp in foreignTextReferencesDictionary)
                {
                    msb.AddLine($"---> Foreign reference to an instance of: {kvp.Key} ");
                    foreach (var fukkkk in kvp.Value)
                    {
                        msb.AddLine($"------> {fukkkk} component.");
                    }
                }
            }
            else
            {
                msb.AddLine($":((((( Has no foreign text references.");
            }
            msb.AddSeparator();
        }

        #endregion // ANALYSIS
    }
}