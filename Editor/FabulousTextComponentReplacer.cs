using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace ZulaMobile.EditorTools
{
    public class FabulousTextComponentReplacer : EditorWindow
    {
        // const string SEARCH_DIRECTORY = "Assets/RemoteAssets";
        const string SEARCH_DIRECTORY = "Assets/Original";

        TextField textField;
        Box boxDisplayer;

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
            boxDisplayer.Add(toDisplay);
        }

        private static TextElement GetTextElement(string textToDisplay)
        {
            var textElement = new TextElement() { text = textToDisplay };
            return textElement;
        }

        public class MultilineStringBuilder
        {
            private StringBuilder builder;

            public string GetString => builder.ToString();

            public MultilineStringBuilder()
            {
                builder = new StringBuilder();
            }

            public MultilineStringBuilder(string titleLine)
            {
                builder = new StringBuilder();
                builder.Append(titleLine);
                AddSeparator();
            }

            public void AddLine(string line)
            {
                builder.Append($"{line} \n");
            }

            public void AddLine(string[] elements)
            {
                builder.Append($"{string.Join("", elements)} \n");
            }

            public void AddSeparator()
            {
                builder.Append($"-------------------------- \n\n");
            }
        }

        private void OnEnable()
        {
            // Reference to the root of the window
            var root = rootVisualElement;

            var assets = AssetDatabase.FindAssets("t:Object", new[] { SEARCH_DIRECTORY });

            DrawTestTextReplacer(root);

            var loadAllAssetsListButton = new Button(() => ListAllFoundAssets(root, assets))
            { text = "Load All Assets List" };
            var listOnlyTopAssetsButton = new Button(() => ListOnlyTopAssets(root, assets))
            { text = "Load Only Top Assets" };

            root.Add(loadAllAssetsListButton);
            root.Add(listOnlyTopAssetsButton);

            boxDisplayer = new Box();
            root.Add(boxDisplayer);
        }

        private void DrawTestTextReplacer(VisualElement root)
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
            }) { text = "Do magic button" };

            var analyseprefabbuttin = new Button(() =>
            {
                var msb = new MultilineStringBuilder("Prefab analysis");

                // var obj = gameobjPreview.value as GameObject;
                var obj = (GameObject)AssetDatabase.LoadAssetAtPath("Assets/Original/Prefabs/PrefabbedParentReferencingDeeplyNestedKid.prefab", typeof(GameObject));
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(obj);


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
                            msb.AddLine( new[] { "NESTED ", t.name.ToString(), " parent: ", componentParent is null ? "<<null>>" : componentParent.name, $" is root: {isRoot}" } );
                        }
                        else
                        {
                            msb.AddLine( new[] { "NOT NESTED ", t.name.ToString(), " parent: ", componentParent is null ? "<<null>>" : componentParent.name, $" is root: {isRoot}" } );
                        }
                    }
                    else
                    {
                        msb.AddLine( new[] { t.name.ToString(), " is not prefabbed" } );
                    }
                }
                DisplayInBox(GetTextElement(msb.GetString));
            }) { text = "Analyse prefab" };

            root.Add(domagicbutton);
            root.Add(analyseprefabbuttin);
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