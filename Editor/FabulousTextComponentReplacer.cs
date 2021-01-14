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

namespace FabulousReplacer
{
    public partial class FabulousTextComponentReplacer : EditorWindow
    {
        const string SEARCH_DIRECTORY = "Assets/Original";
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
        Dictionary<Type, List<GameObject>> _scriptReferences;
        Dictionary<GameObject, List<Type>> _scriptsByPrefab;
        Dictionary<string, string> _scriptCopies;
        bool isBackupMade;

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

        private static TextElement GetTextElement(string textToDisplay)
        {
            var textElement = new TextElement() { text = textToDisplay };
            return textElement;
        }

        private void OnEnable()
        {
            // Reference to the root of the window
            var root = rootVisualElement;

            string[] assets = AssetDatabase.FindAssets("t:Object", new[] { SEARCH_DIRECTORY });

            DrawInitializeSection(root);
            DrawTestTextReplacer(root, assets);
            DrawLoggingButtons(root);

            // var loadAllAssetsListButton = new Button(() => ListAllFoundAssets(root, assets))
            // { text = "Load All Assets List" };
            // var listOnlyTopAssetsButton = new Button(() => ListOnlyTopAssets(root, assets))
            // { text = "Load Only Top Assets" };

            // root.Add(loadAllAssetsListButton);
            // root.Add(listOnlyTopAssetsButton);

            boxDisplayer = new Box();
            root.Add(boxDisplayer);
        }

        private void DrawTestTextReplacer(VisualElement root, string[] assets)
        {
            string testAsset = "Assets/RemoteAssets/UI/ActionPopups/FriendActionPopupView.prefab";

            var label = new Label() { text = "Test Replacer" };
            root.Add(label);
            var objPreview = new ObjectField { objectType = typeof(RectTransform) };
            root.Add(objPreview);
            var gameobjPreview = new ObjectField { objectType = typeof(GameObject) };
            root.Add(gameobjPreview);

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
                var msb = new MultilineStringBuilder("Prefab analysis");

                foreach (var prefab in _loadedPrefabs)
                {
                    AnalyzePrefab(prefab, _loadedPrefabs, msb);
                }

                DisplayInBox(GetTextElement(msb.ToString()));
            })
            { text = "Analyse prefabs" };

            var clearBackupButton = new Button(() =>
            {
                ClearAndRevertBackup();
            })
            { text = "Clear backup" };
            root.Add(clearBackupButton);

            // root.Add(domagicbutton);
            root.Add(analyseprefabbuttin);
        }

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
                Debug.Log(isBackupMade);
                string prefabSearchDirectory = isBackupMade ? PREFABS_COPY_LOCATION : SEARCH_DIRECTORY;
                LoadAllPrefabs(prefabSearchDirectory);
                FindCrossPrefabReferences(depthSearchIntField.value);
                FindScriptReferences(depthSearchIntField.value);
            })
            { text = "Initialize" };

            root.Add(initializeButton);
        }

        private void PrepareCopiesAndBackup()
        {
            ClearAndRevertBackup();

            // foreach (var ass)
            // AssetDatabase.FindAssets()
            // AssetDatabase.CopyAsset()
            // AssetDatabase.

            string path = AssetDatabase.GUIDToAssetPath( PREFABS_ORIGINAL_LOCATION );
            string[] assetsToCopy = AssetDatabase.FindAssets("", path);
            Debug.Log(assetsToCopy.Length);

            foreach (var asset in assetsToCopy)
            {
                AssetDatabase.CopyAsset(asset, PREFABS_COPY_LOCATION);
            }

            // todo you cant copyyyy like that baby
            // ! delete that file util thingy
            FileUtil.CopyFileOrDirectory(PREFABS_ORIGINAL_LOCATION, PREFABS_COPY_LOCATION);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

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

        private void LoadAllPrefabs(string searchLocation)
        {
            _loadedPrefabs = new List<GameObject>();

            string[] assets = AssetDatabase.FindAssets("t:Object", new[] { searchLocation });

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

            foreach (var rootPrefab in _loadedPrefabs)
            {
                if (searchDepth == -1 || currentDepth < searchDepth) currentDepth++;
                else return;

                // var foundNestedPrefabs = new List<GameObject>();
                // rootPrefab.CheckHierarchyForNestedPrefabs(foundNestedPrefabs);

                var foundNestedPrefabs = rootPrefab.CheckHierarchyForNestedPrefabs();

                if (foundNestedPrefabs.Count() > 0)
                {
                    foreach (var nestedPrefab in foundNestedPrefabs)
                    {
                        //! This has to be done since nested prefabs are separate instances
                        // Than those we loaded with LoadAllPrefabs

                        var loadedInstance = _loadedPrefabs.FindInstanceOfTheSamePrefab(nestedPrefab);

                        // var nestedPrefabAsOriginal = nestedPrefab.AsOriginalPrefab();
                        if (loadedInstance == null)
                        {
                            Debug.LogError($"Failed to find {nestedPrefab.name} in loadedPrefabs");
                            MultilineStringBuilder msb = new MultilineStringBuilder("hilde");
                            foreach (var prefab in _loadedPrefabs)
                            {
                                msb.AddLine(prefab.name);
                            }
                            Debug.Log(msb.ToString());
                            continue;
                        }

                        if (!_crossPrefabReferences.ContainsKey(loadedInstance))
                        {
                            _crossPrefabReferences[loadedInstance] = new List<GameObject>();
                        }

                        _crossPrefabReferences[loadedInstance].Add(rootPrefab);
                    }
                }
            }
        }

        private void FindScriptReferences(int searchDepth = -1)
        {
            int currentDepth = 0;

            _scriptReferences = new Dictionary<Type, List<GameObject>>();
            _scriptsByPrefab = new Dictionary<GameObject, List<Type>>();

            foreach (var prefab in _loadedPrefabs)
            {
                if (searchDepth == -1 || currentDepth < searchDepth) currentDepth++;
                else return;

                var foundScripts = new List<MonoBehaviour>();

                prefab.FindScriptsInHierarchy(foundScripts);

                _scriptsByPrefab[prefab] = foundScripts.Select((instance) => instance.GetType()).ToList();

                if (foundScripts.Count > 0)
                {
                    foreach (var mono in foundScripts)
                    {
                        Type monoType = mono.GetType();

                        if (!_scriptReferences.ContainsKey(monoType))
                        {
                            _scriptReferences[monoType] = new List<GameObject>();
                        }

                        // todo here, I am not sure of that skip, maybe we could directly save instances to dictionaries
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
            // box.style.height = StyleKeyword.Auto;
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

        private void AnalyzePrefab(GameObject prefab, List<GameObject> foundPrefabs, MultilineStringBuilder msb)
        {
            List<Text> foundTextComponents = new List<Text>();
            List<MonoBehaviour> foundMonobehaviours = new List<MonoBehaviour>();

            CheckForComponent<Text>(prefab, foundTextComponents);
            CheckForComponentForScripts(prefab, foundMonobehaviours);

            List<string> nestedPrefabs = new List<string>();
            Dictionary<Text, List<Component>> textReferences = new Dictionary<Text, List<Component>>();

            //* Finding all text components
            foreach (Transform child in prefab.transform)
            {
                bool isRoot = PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject);

                if (isRoot)
                {
                    // 1. check for a text component in parent
                    // 2. search in all prefabs referencing this prefab if they reference that text component aswell
                    // 3. collect all scripts referencing that text field
                    nestedPrefabs.Add(child.gameObject.name);
                }
                else
                {
                    CheckForComponent<Text>(child.gameObject, foundTextComponents);
                    CheckForComponentForScripts(child.gameObject, foundMonobehaviours);
                }
            }

            //! Internal text component references
            //* Considering simplest case when text component is only referenced within a single prefabvb
            foreach (Text text in foundTextComponents)
            {
                foreach (Type monoType in _scriptsByPrefab[prefab.AsOriginalPrefab()])
                {
                    // todo handle situation when such component is used multiple times
                    Component component = prefab.GetComponentInChildren(monoType, true);

                    if (component.IsReferencingComponent(text))
                    {
                        if (textReferences.ContainsKey(text) == false)
                        {
                            textReferences[text] = new List<Component>();
                        }

                        textReferences[text].Add(component);
                    }
                }
            }

            //todo Test overwwrite some field
            var tempmonolist = new List<MonoBehaviour>();
            if (prefab.TryGetScripts(tempmonolist))
            {
                foreach (var mono in tempmonolist)
                {
                    if (mono.GetType().Name.Contains("PublicField"))
                    {
                        Debug.Log($"Found a public field scriptin {prefab.name}");
                        mono.GetType().GetField("SomeOtherString").SetValue(mono, "yay!");
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            msb.AddLine($"Analysis of {prefab.name}");
            if (nestedPrefabs.Count > 0)
            {
                msb.AddLine($"Has nested prefabs:");
                foreach (var n in nestedPrefabs)
                {
                    msb.AddLine($"---> {n}");
                }
            }
            if (foundTextComponents.Count > 0)
            {
                msb.AddLine($"Has text components:");
                foreach (var n in foundTextComponents)
                {
                    msb.AddLine($"---> {n.gameObject}");
                }
            }
            if (foundMonobehaviours.Count > 0)
            {
                msb.AddLine($"Has custom monobehaviours:");
                foreach (Type monoType in _scriptsByPrefab[prefab.AsOriginalPrefab()])
                {
                    msb.AddLine($"---> {monoType.Name}");
                }
            }
            if (textReferences.Count > 0)
            {
                msb.AddLine($"Has text references:");
                foreach (var kvp in textReferences)
                {
                    msb.AddLine($"---> {kvp.Key} is referenced at:");
                    foreach (var fukkkk in kvp.Value)
                    {
                        msb.AddLine($"------> {fukkkk} has reference to");
                    }
                }
            }
            msb.AddSeparator();
        }

        // todo trahs tremove
        private static void NewMethod(MultilineStringBuilder msb)
        {
            // var obj = gameobjPreview.value as GameObject;
            var obj = (GameObject)AssetDatabase.LoadAssetAtPath("Assets/Original/Prefabs/PrefabbedParentReferencingDeeplyNestedKid.prefab", typeof(GameObject));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(obj);

            // List<Text> foundTextComponents = new List<Text>();
            // List<MonoBehaviour> foundMonobehaviours = new List<MonoBehaviour>();

            // CheckForComponent<Text>(instance.transform, foundTextComponents);
            // CheckForComponent<MonoBehaviour>(instance.transform, foundMonobehaviours);

            // foreach (Transform child in instance.transform)
            // {
            //     bool isRoot = PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject);

            //     if (isRoot)
            //     {

            //     }
            //     else
            //     {
            //         CheckForComponent<Text>(child, foundTextComponents);
            //         CheckForComponentForScripts(child.gameObject, foundMonobehaviours);
            //     }
            // }

            // todo on monday: actually do that thing for all children (and self) of a top prefab object
            // todo in order to hunt for nested prefabs
            // todo consider: that would be madness but it is possible that a nested prefab contains a reference to the text component of a parent
            // todo if nested prefab: dig in
            // todo if normal text: replace (thats assuming its not referenced somewhere else)
            foreach (Text t in instance.GetComponentsInChildren<Text>())
            {
                if (PrefabUtility.IsPartOfAnyPrefab(t)) // this will actually always return true in our case
                {
                    var isRoot = PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject);

                    var componentParent = PrefabUtility.GetNearestPrefabInstanceRoot(t);
                    if (isRoot)
                    {
                        msb.AddLine(new[] { "NESTED ", t.name.ToString(), " parent: ", componentParent is null ? "<<null>>" : componentParent.name, $" is root: {isRoot}" });
                    }
                    else
                    {
                        msb.AddLine(new[] { "NOT NESTED ", t.name.ToString(), " parent: ", componentParent is null ? "<<null>>" : componentParent.name, $" is root: {isRoot}" });
                    }
                }
                else
                {
                    msb.AddLine(new[] { t.name.ToString(), " is not prefabbed" });
                }
            }
        }

        private static bool CheckForComponent<T>(GameObject go, List<T> foundT) where T : Component
        {
            bool foundSometing = false;

            if (go.TryGetComponent<T>(out T component))
            {
                foundT.Add(component);
                foundSometing = true;
            }

            return foundSometing;
        }

        private static bool CheckForComponentForScripts(GameObject go, List<MonoBehaviour> foundScripts)
        {
            bool foundSometing = false;

            go.GetComponents<MonoBehaviour>().ToList().ForEach((mono) =>
            {
                if (mono.GetType().Namespace.Contains("UnityEngine") == false)
                {
                    foundScripts.Add(mono);
                    foundSometing = true;
                }
            });

            return foundSometing;
        }

        private void ListOnlyTopAssets(VisualElement root, string[] assets)
        {
            var label = new Label() { text = "Top Assets" };
            var foldout = new Foldout() { value = false };
            var scrollview = new ScrollView();

            root.Add(label);
            foldout.Add(scrollview);

            StringBuilder stringbuilder = new StringBuilder();
            int count = 0;
            int textCount = 0;

            for (int i = 0; i < assets.Length; i++)
            {
                string objectsPath = AssetDatabase.GUIDToAssetPath(assets[i]);

                UnityEngine.Object topAsset = AssetDatabase.LoadAssetAtPath(objectsPath, typeof(Component));
                try
                {
                    if (topAsset is Component go)
                    {
                        var textChildren = go.GetComponentsInChildren<Text>();

                        if (textChildren.Length > 0)
                        {
                            stringbuilder.Append($"c: {go.name} cc: {go.gameObject.transform.hierarchyCount} tc: {textChildren.Length} \n");
                            count++;
                            textCount += textChildren.Length;
                            continue;
                        }
                    }
                }
                catch (Exception) {/* I don't care about those two ghostly objects that exist and yet they don't */ }
            }

            var resources = Resources.FindObjectsOfTypeAll(typeof(Component)) as Component[];
            Debug.Log($"resources: {resources.Length}");

            stringbuilder.Append($"\n count: {count.ToString()}");
            stringbuilder.Append($"\n text count: {textCount.ToString()}");

            var textElement = new TextElement() { text = stringbuilder.ToString() };
            scrollview.Add(textElement);
            root.Add(foldout);
        }

        private static void ListAllFoundAssets(VisualElement root, string[] assets)
        {
            var types = new Dictionary<Type, int>();

            types[typeof(Nullable)] = 0;

            for (int i = 0; i < assets.Length; i++)
            {
                string objectsPath = AssetDatabase.GUIDToAssetPath(assets[i]);
                LoadAllAssetsAtPath(types, objectsPath);
            }

            var resources = Resources.FindObjectsOfTypeAll(typeof(Component)) as Component[];
            Debug.Log($"resources: {resources.Length}");

            string final = "";

            final += $"total assets: {assets.Length} \n";

            foreach (var slot in types)
            {
                final += $"{slot.Key}: {slot.Value} \n";
            }

            var foldout = new Foldout() { value = false };
            var scrollview = new ScrollView();
            var textElement = new TextElement() { text = final };

            //foldout.Add(scrollview);
            scrollview.Add(textElement);

            //root.Add(foldout);
            root.Add(scrollview);
        }

        private static void LoadAllAssetsAtPath(Dictionary<Type, int> types, string objectsPath)
        {
            UnityEngine.Object[] objects = AssetDatabase.LoadAllAssetsAtPath(objectsPath);

            foreach (var obj in objects)
            {
                try
                {
                    Type typee = obj.GetType();

                    if (obj is Component)
                    {
                        if (!types.Keys.Contains(typee))
                        {
                            types.Add(typee, 1);
                        }
                        else
                        {
                            types[typee] += 1;
                        }
                    }
                }
                catch (Exception)
                {
                    Debug.Log("Caught exception");
                }
            }

            //Debug.Log($"keys: {types.Keys.Count}");
            //Debug.Log($"values: {types.Values.Count}");

            //foreach (var obj in objects)
            //{
            //    Type typee = typeof(Nullable);

            //    try
            //    {
            //        typee = obj.GetType();

            //        if (obj is UnityEngine.UI.Text text)
            //        {

            //        }
            //    }
            //    catch (NullReferenceException e)
            //    {
            //        types[typeof(Nullable)] += 1;
            //        Debug.Log(obj);
            //    }

            //    if (!types.Keys.Contains(typee))
            //    {
            //        types.Add(typee, 1);
            //    }
            //    else
            //    {
            //        types[typee] += 1;
            //    }
            //}
        }
    }
}