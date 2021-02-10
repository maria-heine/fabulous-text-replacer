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
        public const int WORK_DEPTH = -1;
        const string SEARCH_DIRECTORY = "Assets/RemoteAssets";
        /*
        ! Note that "Assets/Original/Prefabs"
        ! is entirely different than "Assets/Original/Prefabs/"
        */

        List<GameObject> _loadedPrefabs;
        Dictionary<GameObject, List<GameObject>> _crossPrefabReferences;
        Dictionary<GameObject, List<MonoBehaviour>> _customMonobehavioursByPrefab;
        Dictionary<MonoBehaviour, List<FieldInfo>> _textFieldsByMonobehaviour;
        Dictionary<GameObject, List<GameObject>> _nestedPrefabs;
        Dictionary<Type, List<GameObject>> _scriptReferences;
        Dictionary<GameObject, List<Type>> _scriptsByPrefab;
        ReplaceCounter _replaceCounter;
        ScriptUpdater _scriptUpdater;
        ComponentReplacer _componentReplacer;
        UpdatedReferenceAddressBook _updatedReferenceAddressBook;

        Box _boxDisplayer;
        IntegerField _lowRange;
        IntegerField _highRange;
        ObjectField _selectedPrefabsField;

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

            DrawReplacerMenu(menuBox);
            DrawLoggingButtons(menuBox);
            DrawProgressStatus(menuBox);
        }

        #endregion //  EDITOR WINDOW STARTUP

        //
        // ─── INITIALIZATION ─────────────────────────────────────────────────────────────
        //
        #region Initialization

        private void DrawReplacerMenu(VisualElement root)
        {
            var container = new Box();
            root.Add(container);

            var label = new Label() { text = "Replacer" };
            container.Add(label);

            _selectedPrefabsField = new ObjectField("Selected prefabs")
            {
                objectType = typeof(SelectedPrefabsBook)
            };
            container.Add(_selectedPrefabsField);

            Button initializeButton = new Button(() =>
            {
                //* Where to search for prefabs (depending on whether we make a backup or not)
                // ! Prefab backup abandoned
                UpdatedReferenceAddressBook.ClearAddressBook();
                LoadPrefabs();
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

            _lowRange = new IntegerField("Replacement low range");
            _lowRange.value = 0;
            container.Add(_lowRange);

            _highRange = new IntegerField("Replacement high range");
            _highRange.value = 20;
            container.Add(_highRange);

            var updateScriptsButton = new Button()
            { text = "Update scripts" };
            _scriptUpdater =
                new ScriptUpdater(
                    UpdatedReferenceAddressBook,
                    updateScriptsButton,
                    _lowRange,
                    _highRange);
            container.Add(updateScriptsButton);

            var updateComponentsButton = new Button()
            { text = "Update components" };
            _componentReplacer =
                new ComponentReplacer(
                    UpdatedReferenceAddressBook,
                    updateComponentsButton,
                    _lowRange,
                    _highRange);
            container.Add(updateComponentsButton);

            analysePrefabsButton.clicked += () =>
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

                    if (analysisDepth.value != -1 && currentDepth >= analysisDepth.value) break;

                    AnalyzePrefab(prefab, msb, ref currentDepth);
                }

                analysisResultsParts.Add(msb.ToString());

                UpgradeProgressBar.Invoke();

                DisplayInBox(GetTextBlock(analysisResultsParts));
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

        //
        // ─── PREFAB LOADING ─────────────────────────────────────
        //

        #region PREFAB LOADING

        private void LoadPrefabs()
        {
            _loadedPrefabs = new List<GameObject>();
            _replaceCounter = new ReplaceCounter();

            string[] assetPaths = null;

            if (_selectedPrefabsField.value != null)
            {
                assetPaths = GetSelectedAssetPaths();
            }
            else
            {
                LoadAllPrefabs();
            }

            LoadSelectedAssets(assetPaths);
        }

        private void LoadSelectedAssets(string[] assetPath)
        {
            for (int i = 0; i < assetPath.Length; i++)
            {
                if (CheckAgainstExcludedPaths(assetPath[i]))
                {
                    continue;
                }

                UnityEngine.Object topAsset = AssetDatabase.LoadAssetAtPath(assetPath[i], typeof(Component));

                try
                {
                    // Since only prefabs in assets will have a component as it's root
                    if (topAsset is Component c)
                    {
                        bool hasMonobehaviour = c.GetComponentInChildren<MonoBehaviour>(includeInactive: true) != null;
                        bool hasRectTransform = c.GetComponentInChildren<RectTransform>(includeInactive: true) != null;
                        if (hasRectTransform)
                        {
                            _loadedPrefabs.Add(c.gameObject);
                            // ! Counting all found text components
                            _replaceCounter.totalTextComponentCount += c.gameObject.GetComponentsInChildren<Text>(includeInactive: true).Count();
                        }
                    }
                }
                catch (Exception)
                {
                    /* Because I don't care about those two unnameable ghostly objects that exist and yet they don't */
                    Debug.Log($"Prefabl searcher exception for path: {assetPath[i]}");
                }
            }
        }

        private string[] GetSelectedAssetPaths()
        {
            SelectedPrefabsBook prefabsBook = _selectedPrefabsField.value as SelectedPrefabsBook;

            string[] paths = prefabsBook.SelectedPrefabs.Select(o => AssetDatabase.GetAssetPath(o)).ToArray();

            Debug.Log($"{paths[0]}");
            

            return paths;
        }

        private List<string> ExcludedPaths = new List<string>()
        {
            "Assets/RemoteAssets/UI/InGame/NavigatorHUD/NavigatorHUDView.prefab",
            "Assets/RemoteAssets/UI/InGame/QuickChatHUD/QuickChatPanelHUDView.prefab",
            "Assets/RemoteAssets/UI/Popup/CardRarityBonusPopup.prefab"
        };

        //! This is the dirtiest thing in the entire replacer code here, I am sorry for that, sacrifices had to be made
        private bool CheckAgainstExcludedPaths(string path)
        {
            foreach (string excludedPath in ExcludedPaths)
            {
                if (path == excludedPath)
                {
                    Debug.Log($"Excluding {path}");
                    return true;
                }
            }

            return false;
        }

        private void LoadAllPrefabs()
        {
            /* 
            * Note: This will count both directories and script files as assets
            */
            string[] assets = AssetDatabase.FindAssets("t:Object", new[] { SEARCH_DIRECTORY });

            Debug.Log($"Found {assets.Length} assets.");

            for (int i = 0; i < assets.Length; i++)
            {
                string objectsPath = AssetDatabase.GUIDToAssetPath(assets[i]);

                if (CheckAgainstExcludedPaths(objectsPath))
                {
                    continue;
                }

                UnityEngine.Object topAsset = AssetDatabase.LoadAssetAtPath(objectsPath, typeof(Component));

                try
                {
                    // Since only prefabs in assets will have a component as it's root
                    if (topAsset is Component c)
                    {
                        bool hasMonobehaviour = c.GetComponentInChildren<MonoBehaviour>(includeInactive: true) != null;
                        bool hasRectTransform = c.GetComponentInChildren<RectTransform>(includeInactive: true) != null;
                        if (hasRectTransform)
                        {
                            _loadedPrefabs.Add(c.gameObject);
                            // ! Counting all found text components
                            _replaceCounter.totalTextComponentCount += c.gameObject.GetComponentsInChildren<Text>(includeInactive: true).Count();
                        }
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

                        //TODO This has to be done with msb logger instead
                        //TODO Note there are obviously many overrides so this may cause problewms 
                        if (overrides.Count > 0)
                        {
                            //Debug.Log($"{rootPrefab} has overrides at {nestedPrefab}");
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

        #endregion // Prefab Loading

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

            var logRange = new Button(() =>
            {
                var msb = new MultilineStringBuilder("Log prefabs in update range");

                msb.AddLine($"Total length of address book is {UpdatedReferenceAddressBook.Paths.Length}");

                for (int i = _lowRange.value; i < _highRange.value; i++)
                {
                    msb.AddLine(UpdatedReferenceAddressBook.Paths[i]);
                }

                DisplayInBox(GetTextElement(msb.ToString()));
            })
            { text = "Log prefabs in update range" };
            box.Add(logRange);

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

        //
        // ─── ANALYSIS ────────────────────────────────────────────────────
        //

        #region ANALYSIS

        private void AnalyzePrefab(GameObject originalPrefab, MultilineStringBuilder msb, ref int currentDepth)
        {
            // Skips execition if there were no text components found in the hierarchy
            if (!originalPrefab.TryGetComponentsInChildren(out List<Text> localTextComponents, skipNestedPrefabs: true))
            {
                return;
            }
            else
            {
                currentDepth++;
            }

            bool logthisone = true;

            foreach (Text text in localTextComponents)
            {
                string originalPrefabPath = AssetDatabase.GetAssetPath(originalPrefab);

                // * Always add 
                // TODO test disabled
                // UpdatedReference unreferencedTextComponent = new UpdatedReference(originalPrefab, text);
                // UpdatedReferenceAddressBook[originalPrefabPath].Add(unreferencedTextComponent);

                // 1. Internal text component references
                // Considering simplest case when text component is only referenced within a single prefabvb
                SaveTextReferences(originalPrefabPath, originalPrefab, text);

                // 2. Text components referenced by nested prefabs
                // 3. Text components of prefabs that are reference by the other nested prefabs
                // X???. Text components of prefabs that are referenced by parent of a parent OR by a nested prefab of a parent of a parent OOF

                // 4. Foreign text component references
                // Get all instances of a Text component along with the parentPrefab that is holding them
                // It is a list of Text components because parentPrefab may hold multiple references to that Text Component
                foreach (var kvp in GetAllTextInstances(originalPrefab, text))
                {
                    // * Clean this up, extract the recurring method
                    GameObject instanceParentPreafab = kvp.Key;
                    List<Text> textComponentInstances = kvp.Value;

                    foreach (Text textInstance in textComponentInstances)
                    {
                        SaveTextReferences(originalPrefabPath, instanceParentPreafab, textInstance);

                        // TODO take care of logging if needed
                        // logthisone = UpdateCountLogger(logthisone, textInstanceReferences);

                        // ? This is because the counter will
                        _replaceCounter.updatedTextComponentCount++;
                    }
                }
            }

            if (logthisone) PrintPrefabAnalysis(originalPrefab, msb, localTextComponents);
        }

        /// <summary>
        /// Core logic of gathering a dictionary of parent prefab GameObjects along with a list of text component instances
        /// of a passed in originalPrefab and it's text component.
        /// I admit this could have been refactored to look cleaner than this maddness.
        /// The difficulty of algorithm could be simplified probably by doing it in a barch for all the text components of the 
        /// original prefab at once, but well, we aren't dealing with millions of components.
        /// </summary>
        /// <param name="originalPrefab">The original prefab we want to look for in other prefabs</param>
        /// <param name="originalText">It's text component we are interested in</param>
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

        private void SaveTextReferences(string sourcePrefabPath, GameObject prefabParent, Text textComponent)
        {
            // Separately add each text component as unreferenced version
            UpdatedReference unreferencedTextComponent = new UpdatedReference(prefabParent, textComponent);
            UpdatedReferenceAddressBook[sourcePrefabPath].Add(unreferencedTextComponent);

            var updatedReferences = CheckTextAgainstFoundMonobehaviours(textComponent, prefabParent);
            foreach (UpdatedReference updatedReference in updatedReferences)
            {
                UpdatedReferenceAddressBook[sourcePrefabPath].Add(updatedReference);
            }
        }

        private List<UpdatedReference> CheckTextAgainstFoundMonobehaviours(Text textComponent, GameObject parentPrefab)
        {
            List<MonoBehaviour> monoBehaviours = _customMonobehavioursByPrefab[parentPrefab];
            List<UpdatedReference> updatedReferences = new List<UpdatedReference>(monoBehaviours.Count);

            foreach (MonoBehaviour mono in monoBehaviours)
            {
                if (mono.GetType().Name.Contains("ChatButton"))
                {
                    Debug.Log($"mono: {mono} text: {textComponent.text}");
                }
                if (mono.IsReferencingComponent(anotherComponent: textComponent, out string fieldName))
                {
                    if (mono.GetType().Name.Contains("ChatButton"))
                    {
                    Debug.Log($"saved!");
                    }
                    //! should be original prefab instead
                    updatedReferences.Add(new UpdatedReference(parentPrefab, textComponent, mono, fieldName));
                    _replaceCounter.updatedTextComponentReferencesCount++;
                }
            }

            return updatedReferences;
        }

        // TODO Update this if needed
        [Obsolete]
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

        private void PrintPrefabAnalysis(GameObject prefab, MultilineStringBuilder msb, List<Text> localTextComponents)
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

            // foreach (var textRef in textRefs)
            // {
            //     msb.AddLine($"Text at: {textRef.originalPrefabText.gameObject.name}:");

            //     if (textRef.localTextReferences != null && textRef.localTextReferences.Count > 0)
            //     {
            //         msb.AddLine($"---> Has internal references:");
            //         foreach (var fukkkk in textRef.localTextReferences)
            //         {
            //             msb.AddLine($"------> {fukkkk}");
            //         }
            //     }

            //     if (textRef.foreignTextReferencesDictionary != null && textRef.foreignTextReferencesDictionary.Count > 0)
            //     {
            //         msb.AddLine($"(!!!) Has foreign text references:");
            //         foreach (var kvp in textRef.foreignTextReferencesDictionary)
            //         {
            //             msb.AddLine($"---> Foreign reference to an instance of: {kvp.Key} ");
            //             foreach (var fukkkk in kvp.Value)
            //             {
            //                 msb.AddLine($"------> {fukkkk} component.");
            //             }
            //         }
            //     }
            // }

            msb.AddSeparator();
        }

        #endregion // ANALYSIS
    }
}