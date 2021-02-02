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
        public const int WORK_DEPTH = 1;
        const string SEARCH_DIRECTORY = "Assets/RemoteAssets";
        /*
        ! Note that "Assets/Original/Prefabs"
        ! is entirely different than "Assets/Original/Prefabs/"
        */
        const string PREFABS_ORIGINAL_LOCATION = "Assets/Original/Prefabs";
        const string PREFABS_COPY_LOCATION = "Assets/Copy/Prefabs";

        List<GameObject> _loadedPrefabs;
        Dictionary<GameObject, List<GameObject>> _crossPrefabReferences;
        Dictionary<GameObject, List<MonoBehaviour>> _customMonobehavioursByPrefab;
        Dictionary<MonoBehaviour, List<FieldInfo>> _textFieldsByMonobehaviour;
        Dictionary<GameObject, List<GameObject>> _nestedPrefabs;
        Dictionary<Type, List<GameObject>> _scriptReferences;
        Dictionary<GameObject, List<Type>> _scriptsByPrefab;
        ReplaceCounter _replaceCounter;
        ComponentReplacer _componentReplacer;
        ReferenceUpdater _referenceUpdater;
        UpdatedReferenceAddressBook _updatedReferenceAddressBook;
        Box _boxDisplayer;

        Action UpgradeProgressBar;

        UpdatedReferenceAddressBook UpdatedReferenceAddressBook
        {
            get
            {
                if (_updatedReferenceAddressBook == null)
                {
                    string addressBookPath = "Packages/com.mariaheineboombyte.fabulous-text-replacer/Editor/Scriptable/UpdatedReferenceAddressBook.asset";
                    UnityEngine.Object addressBook = AssetDatabase.LoadAssetAtPath(addressBookPath, typeof(UpdatedReferenceAddressBook));
                    _updatedReferenceAddressBook = addressBook as UpdatedReferenceAddressBook;
                }

                return _updatedReferenceAddressBook;
            }
        }

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
            _boxDisplayer.Clear();
            var scrollview = new ScrollView();
            scrollview.Add(toDisplay);
            _boxDisplayer.Add(scrollview);
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
            _boxDisplayer = new Box();
            dataBox.Add(_boxDisplayer);

            DrawReplacerButtons(menuBox);
            DrawLoggingButtons(menuBox);
            DrawProgressStatus(menuBox);
        }

        #endregion //  EDITOR WINDOW STARTUP

        //
        // ─── INITIALIZATION ─────────────────────────────────────────────────────────────
        //
        #region Initialization

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

        private void FindCrossPrefabReferences()
        {
            _crossPrefabReferences = new Dictionary<GameObject, List<GameObject>>();
            _nestedPrefabs = new Dictionary<GameObject, List<GameObject>>();

            foreach (var rootPrefab in _loadedPrefabs)
            {

                List<GameObject> foundNestedPrefabs = rootPrefab.CheckHierarchyForNestedPrefabs();

                if (foundNestedPrefabs.Count() > 0)
                {
                    _nestedPrefabs.Add(rootPrefab, foundNestedPrefabs);

                    bool foundErrors = false;
                    MultilineStringBuilder msb = new MultilineStringBuilder($"Cross Prefab Reference serach, load failures f or <<{rootPrefab.name}>>");

                    foreach (var nestedPrefab in foundNestedPrefabs)
                    {
                        var overrides = PrefabUtility.GetObjectOverrides(nestedPrefab);

                        if (overrides.Count > 0)
                        {
                            Debug.Log($"{rootPrefab} has overrides at {nestedPrefab}");
                        }

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

        private void FindScriptReferences()
        {
            _scriptReferences = new Dictionary<Type, List<GameObject>>();
            _scriptsByPrefab = new Dictionary<GameObject, List<Type>>();
            _customMonobehavioursByPrefab = new Dictionary<GameObject, List<MonoBehaviour>>();
            _textFieldsByMonobehaviour = new Dictionary<MonoBehaviour, List<FieldInfo>>();

            foreach (var prefab in _loadedPrefabs)
            {
                prefab.FindScriptsInHierarchy(out List<MonoBehaviour> foundScripts);
                _customMonobehavioursByPrefab[prefab] = foundScripts;

                // TODO I think this is not used anymore
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

                        if (mono.TryGetAllFieldsOfType<Text>(out List<FieldInfo> foundFields))
                        {
                            _textFieldsByMonobehaviour.Add(mono, foundFields);
                            _replaceCounter.totalTextComponentReferencesCount += foundFields.Count;
                        }

                        // TODO Remeber to later find all instances of such script in a mono since there may be multiple
                        if (_scriptReferences[monoType].Contains(prefab)) continue;

                        _scriptReferences[monoType].Add(prefab);
                    }
                }
            }
        }
        
        private void DrawReplacerButtons(VisualElement root)
        {
            var container = new Box();
            root.Add(container);

            var label = new Label() { text = "Replacer" };
            container.Add(label);
            
            Button initializeButton = new Button(() =>
            {
                //* Where to search for prefabs (depending on whether we make a backup or not)
                // ! Prefab backup abandoned
                UpdatedReferenceAddressBook.ClearAddressBook();
                LoadAllPrefabs();
                FindCrossPrefabReferences();
                FindScriptReferences();
                UpgradeProgressBar.Invoke();
            })
            { text = "Initialize" };
            container.Add(initializeButton);

            IntegerField analysisDepth = new IntegerField("Prefab Analysis Depth");
            analysisDepth.value = FabulousTextComponentReplacer.WORK_DEPTH;
            container.Add(analysisDepth);

            var analysePrefabsButton = new Button()
            { text = "Analyse prefabs" };
            container.Add(analysePrefabsButton);

            var updateComponentsButton = new Button()
            { text = "Update components" };
            _componentReplacer = new ComponentReplacer(UpdatedReferenceAddressBook, updateComponentsButton);
            container.Add(updateComponentsButton);

            var referenceUpdateButton = new Button()
            { text = "Update references" };
            _referenceUpdater = new ReferenceUpdater(UpdatedReferenceAddressBook, referenceUpdateButton);
            container.Add(referenceUpdateButton);

            analysePrefabsButton.clicked += () =>
            {
                List<TextRefernce> textReferences = new List<TextRefernce>();

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

                    if (analysisDepth.value != -1 && currentDepth >= analysisDepth.value) break;

                    List<TextRefernce> resultReferences = AnalyzePrefab(prefab, msb, ref currentDepth);

                    if (resultReferences != null)
                    {
                        textReferences.AddRange(resultReferences);
                    }
                }

                analysisResultsParts.Add(msb.ToString());

                UpgradeProgressBar.Invoke();

                DisplayInBox(GetTextBlock(analysisResultsParts));

                _componentReplacer.SetReplacerTextReferences(textReferences);
            };
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

            var logOverridesButton = new Button(() =>
            {
                var msb = new MultilineStringBuilder("Log nested prefabs with overrides");
                
                foreach (var item in _nestedPrefabs)
                {
                    bool loggedOverride = false;

                    foreach (var nested in item.Value)
                    {
                        bool hasOverrides = PrefabUtility.HasPrefabInstanceAnyOverrides(nested, false);

                        if (hasOverrides && loggedOverride == false)
                        {
                            loggedOverride = true;
                            msb.AddLine($"{item.Key.gameObject.name} has overrides at:");
                        }
                        
                        if (hasOverrides)
                        {
                            msb.AddLine($"---> {nested.name}");
                        }
                    }

                    loggedOverride = false;
                }

                DisplayInBox(GetTextElement(msb.ToString()));
            })
            { text = "Log overrides" };
            box.Add(logOverridesButton);

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

            // TODO this is dead
            var logUnhandledReferences = new Button(() =>
            {
                var msb = new MultilineStringBuilder("Unhandled script references");

                foreach (var kvp in _textFieldsByMonobehaviour)
                {
                    msb.AddLine(new string[] { kvp.Key.GetType().ToString(), " from ", _customMonobehavioursByPrefab.First(slot => slot.Value.Contains(kvp.Key)).Key.ToString() });
                    foreach (var unhandledField in kvp.Value)
                    {
                        msb.AddLine($"---> {unhandledField.Name}");
                    }
                }

                DisplayInBox(GetTextElement(msb.ToString()));
            })
            { text = "Unhandled script references" };
            box.Add(logUnhandledReferences);
        }

        private void LogCrossReferences(MultilineStringBuilder msb, int logDepth = 30)
        {
            int depth = 0;

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
        /// <returns>Returns a dictionary of prefabs referencing an instance of the originalPrefab with a list of it's specific Text components being referenced</returns>
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
                            catch (Exception)
                            {
                                Debug.LogError($"Failed to get duplicate of a {originalText.name} component of an original <<{originalPrefab.name}>> gameobject and it's duplicate <<{referencerNestedPrefab.name}>> in <<{thisPrefabReferncer}>>");
                            }
                        }
                    }
                }
            }

            return prefabTextInstances;
        }

        private List<TextRefernce> AnalyzePrefab(GameObject prefab, MultilineStringBuilder msb, ref int currentDepth)
        {
            // Skips execition if there were no text components found in the hierarchy
            if (!prefab.TryGetComponentsInChildren(out List<Text> localTextComponents, skipNestedPrefabs: true))
            {
                return null;
            }
            else
            {
                currentDepth++;
            }

            bool logthisone = true;

            List<TextRefernce> textRefernces = new List<TextRefernce>(localTextComponents.Count);

            foreach (Text text in localTextComponents)
            {
                // bool isReferenced = false;
                string prefabPath = AssetDatabase.GetAssetPath(prefab);

                    if (prefab.name.Contains("PrefabbedText"))
                    {
                        Debug.Log(prefab);
                    };

                // * Always add 
                UpdatedReference unreferencedTextComponent = new UpdatedReference(prefab, text);
                UpdatedReferenceAddressBook[prefabPath].Add(unreferencedTextComponent);

                TextRefernce textRef = new TextRefernce(prefabPath, text);
                textRefernces.Add(textRef);
                _replaceCounter.updatedTextComponentCount++;

                //! 1. Internal text component references
                //* Considering simplest case when text component is only referenced within a single prefabvb

                foreach (MonoBehaviour mono in _customMonobehavioursByPrefab[prefab])
                {
                    if (mono.IsReferencingComponent(anotherComponent: text, out string fieldName))
                    {
                        UpdatedReference updatedAsstReference = new UpdatedReference(prefab, text, mono, fieldName);
                        UpdatedReferenceAddressBook[prefabPath].Add(updatedAsstReference);
                        // isReferenced = true;
                    }
                }

                // TODO remove this thing, it only does the counitng now
                if (prefab.TryExtractTextReferences(text, _customMonobehavioursByPrefab[prefab], out List<MonoBehaviour> textReferences))
                {
                    textRef.SetLocalTextReferences(textReferences);
                    logthisone = UpdateCountLogger(logthisone, textReferences);

                    _replaceCounter.updatedTextComponentReferencesCount += textReferences.Count;
                }

                //todo 2. Text components referenced by nested prefabs
                //todo 3. Text components of prefabs that are reference by the other nested prefabs
                //todo X???. Text components of prefabs that are referenced by parent of a parent OR by a nested prefab of a parent of a parent OOF

                //! 4. Foreign text component references
                // Get all instances of a Text component along with the parentPrefab that is holding them
                // It is a list of Text components because parentPrefab may hold multiple references to that Text Component
                foreach (var kvp in GetAllTextInstances(prefab, text))
                {
                    // * Clean this up, extract the recurring method
                    GameObject parentPrefab = kvp.Key;
                    List<Text> textComponentInstances = kvp.Value;

                    string otherPrefabPath = AssetDatabase.GetAssetPath(parentPrefab);

                    foreach (Text textInstance in textComponentInstances)
                    {
                        foreach (MonoBehaviour mono in _customMonobehavioursByPrefab[parentPrefab])
                        {
                            if (mono.IsReferencingComponent(anotherComponent: textInstance, out string fieldName))
                            {
                                //! should be original prefab instead
                                UpdatedReference updatedAsstReference = new UpdatedReference(parentPrefab, textInstance, mono, fieldName);
                                UpdatedReferenceAddressBook[prefabPath].Add(updatedAsstReference);
                                // isReferenced = true;
                            }
                        }

                        // TODO Remove all below
                        bool foundMonobehaviourReferences = parentPrefab
                            .TryExtractTextReferences(
                                text: textInstance,
                                monoBehaviourToCheck: _customMonobehavioursByPrefab[parentPrefab],
                                textReferences: out List<MonoBehaviour> textInstanceReferences);

                        if (foundMonobehaviourReferences)
                        {
                            textRef.AddForeignTextReference(textInstance, textInstanceReferences);
                            _replaceCounter.updatedTextComponentReferencesCount += textInstanceReferences.Count;
                        }
                        else
                        {
                            // TODO  Just add a reference to a found text instance that we didn't find other scipts referencing it
                            textRef.AddUnreferencedTextInstance(textInstance);
                        }

                        logthisone = UpdateCountLogger(logthisone, textInstanceReferences);

                        _replaceCounter.updatedTextComponentCount++;
                    }
                }
            }

            if (logthisone) PrintPrefabAnalysis(prefab, textRefernces, msb, localTextComponents);

            return textRefernces;
        }

        private bool UpdateCountLogger(bool logthisone, List<MonoBehaviour> textReferences)
        {
            // * Below is just for counter purposes
            foreach (var monoRef in textReferences)
            {
                monoRef.TryGetAllFieldsOfType<Text>(out List<FieldInfo> foundTFields);
                foreach (var field in foundTFields)
                {
                    try
                    {
                        _textFieldsByMonobehaviour[monoRef].Remove(field);
                        if (_textFieldsByMonobehaviour[monoRef].Count == 0)
                        {
                            _textFieldsByMonobehaviour.Remove(monoRef);
                        }
                    }
                    catch (Exception)
                    {
                        logthisone = true;
                    }
                }
            }

            return logthisone;
        }

        private void PrintPrefabAnalysis(GameObject prefab, List<TextRefernce> textRefs, MultilineStringBuilder msb, List<Text> localTextComponents)
        {
            msb.AddLine($"Analysis of {prefab.name}");
            if (_nestedPrefabs.ContainsKey(prefab) && _nestedPrefabs[prefab].Count > 0)
            {
                msb.AddLine($"Has nested prefabs:");
                foreach (var n in _nestedPrefabs[prefab])
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
            if (_crossPrefabReferences.ContainsKey(prefab))
            {
                msb.AddLine($"Is referenced by other prefabs:");
                foreach (GameObject referencer in _crossPrefabReferences[prefab])
                {
                    msb.AddLine($"---> by: {referencer}");
                }
            }

            foreach (var textRef in textRefs)
            {
                msb.AddLine($"Text at: {textRef.originalPrefabText.gameObject.name}:");

                if (textRef.localTextReferences != null && textRef.localTextReferences.Count > 0)
                {
                    msb.AddLine($"---> Has internal references:");
                    foreach (var fukkkk in textRef.localTextReferences)
                    {
                        msb.AddLine($"------> {fukkkk}");
                    }
                }

                if (textRef.foreignTextReferencesDictionary != null && textRef.foreignTextReferencesDictionary.Count > 0)
                {
                    msb.AddLine($"(!!!) Has foreign text references:");
                    foreach (var kvp in textRef.foreignTextReferencesDictionary)
                    {
                        msb.AddLine($"---> Foreign reference to an instance of: {kvp.Key} ");
                        foreach (var fukkkk in kvp.Value)
                        {
                            msb.AddLine($"------> {fukkkk} component.");
                        }
                    }
                }
            }

            msb.AddSeparator();
        }

        #endregion // ANALYSIS
    }
}