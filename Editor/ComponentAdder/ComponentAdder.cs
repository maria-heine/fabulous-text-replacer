using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using static FabulousReplacer.FabulousExtensions;

namespace FabulousReplacer
{
    [CreateAssetMenu(fileName = "TestOCF", menuName = "FabulousReplacer/TestOCF")]
    public class TestOCF : OnComponentFound<TextMeshProUGUI>
    {
        public override void DoOnFoundComponent(TextMeshProUGUI Component)
        {
            Component.gameObject.AddComponent(typeof(Outline));
        }
    }

    public abstract class OnComponentFound<T> : ScriptableObject
    {
        public abstract void DoOnFoundComponent(T Component);
    }

    public class ComponentAdder : EditorWindow
    {
        public const string PREFAB_SEARCH_LOCATION = "Assets/RemoteAssets";
        private ObjectField _onComponentField;

        [MenuItem("Window/Zula Mobile/Component Adder")]
        public static void ShowWindow()
        {
            var window = GetWindow<ComponentAdder>();
            window.titleContent = new GUIContent("Component Adder");
            window.minSize = new Vector2(250, 50);
        }

        private void OnEnable()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;
            var menuBox = new Box();
            menuBox.style.alignItems = Align.Stretch;
            menuBox.style.width = new StyleLength(250f);

            _onComponentField = new ObjectField("On component field")
            {
                objectType = typeof(OnComponentFound<TextMeshProUGUI>)
            };

            menuBox.Add(_onComponentField);
            var updateScriptsButton = new UnityEngine.UIElements.Button()
            { text = "Add components" };
            updateScriptsButton.clicked += () => LoadPrefabs();
            menuBox.Add(updateScriptsButton);
            root.Add(menuBox);
        }

        private void LoadPrefabs()
        {
            string[] assetPaths = GetAllAssetPaths(new[] { PREFAB_SEARCH_LOCATION });

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (string path in assetPaths)
                {
                    Type assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                    bool isValidPrefab = assetType == typeof(GameObject);
                    if (!isValidPrefab)
                    {
                        Debug.Log($"<color=yellow>Rejected {assetType} at {path}</color>");
                        continue;
                    }
                    Debug.Log($"{path}");
                    using (var editScope = new EditPrefabAssetScope(path))
                    {
                        GameObject root = editScope.prefabRoot;

                        if (root.TryGetComponentsInChildren<TextMeshProUGUI>(out List<TextMeshProUGUI> textComponents, skipNestedPrefabs: true))
                        {
                            Debug.Log($"Found text components");

                            foreach (var textComponent in textComponents)
                            {
                                (_onComponentField.value as OnComponentFound<TextMeshProUGUI>).DoOnFoundComponent(textComponent);
                            }

                            editScope.SavePrefabOnDispose = true;
                        }
                        else
                        {
                            editScope.SavePrefabOnDispose = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message + "\n" + ex.StackTrace);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        private void LoadSelectedAssets(string[] assetPath, out List<GameObject> loadedPrefabs)
        {
            loadedPrefabs = new List<GameObject>();

            for (int i = 0; i < assetPath.Length; i++)
            {
                UnityEngine.Object topAsset = AssetDatabase.LoadAssetAtPath(assetPath[i], typeof(Component));

                try
                {
                    // Since only prefabs in assets will have a component as it's root
                    if (topAsset is Component c)
                    {
                        bool hasRectTransform = c.GetComponentInChildren<RectTransform>(includeInactive: true) != null;
                        bool hasTMPro = c.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true) != null;

                        if (hasRectTransform && hasTMPro)
                        {
                            loadedPrefabs.Add(c.gameObject);
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

        private string[] GetAllAssetPaths(string[] searchLocation)
        {
            /* 
            * Note: This will count both directories and script files as assets
            */
            string[] assets = AssetDatabase.FindAssets("t:Object", searchLocation);

            Debug.Log($"Found {assets.Length} assets.");

            string[] excludedPaths = null;

            string[] paths = assets
                .Select(asset => AssetDatabase.GUIDToAssetPath(asset))
                .ToArray();

            return paths;
        }
    }
}
