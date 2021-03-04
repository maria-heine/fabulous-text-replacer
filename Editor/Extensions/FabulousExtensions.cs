using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace FabulousReplacer
{
    public static class FabulousExtensions
    {
        //
        // ─── FIELD TYPES ─────────────────────────────────────────────────
        //
            
        #region FIELD TYPES
        /*
        * Gets the type originally declaring a given field in case 
        * the passed type is just a child inheriting that field.
        * This thingy is necessary when some text fields were inherited
        */
        public static Type GetFieldDeclaringType(Type type, string field)
        {
            // Debug.Log($"Searching {type} for {field} field");

            Type originalType = type;

            FieldInfo fieldInfo = type
                .GetFields(ReferenceFinder.GENEROUS_NONSTATIC_FIELD_SEARCH_FLAGS | BindingFlags.DeclaredOnly)
                .Where(f => f.DeclaringType == type)
                .FirstOrDefault(f => f.Name == field);

            if (fieldInfo == null)
            {
                // Debug.Log($"Didn't find, switching to base type of {type.BaseType}");

                type = type.BaseType;

                if (type == null)
                {
                    Debug.LogError("Failed to find enclosing type.");
                    return null;
                }

                type = GetFieldDeclaringType(type, field);
            }

            return type;
        }

        public static bool HasOneOfTheFlags(this FieldType fieldType1, FieldType fieldType2)
        {
            return (fieldType1 &= fieldType2) != 0;
        }

        #endregion // FIELD TYPES

        //
        // ─── EDITOR GUI ──────────────────────────────────────────────────
        //

        #region EDITOR GUI
        public static TextElement GetTextElement(string textToDisplay)
        {
            return new TextElement() { text = textToDisplay };
        }

        public static VisualElement GetTextBlock(List<string> textParts)
        {
            VisualElement el = new VisualElement();

            foreach (var textPiece in textParts)
            {
                el.Add(GetTextElement(textPiece));
            }

            return el;
        }
        #endregion // EDITOR GUI

        //
        // ─── NESTED PREFAB SEARCHING ─────────────────────────────────────
        //

        #region NESTED PREFAB SEARCHING

        public static List<GameObject> CheckHierarchyForNestedPrefabs(this GameObject root)
        {
            List<GameObject> collection = new List<GameObject>();

            foreach (Transform child in root.transform)
            {
                bool isRoot = PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject);

                if (isRoot)
                {
                    collection.Add(child.gameObject);
                }
                else if (child.childCount > 0)
                {
                    var nestedCollection = CheckHierarchyForNestedPrefabs(child.gameObject);
                    collection.AddRange(nestedCollection);
                }
            }

            return collection;
        }

        #endregion // NESTED PREFAB SEARCHING

        //
        // ─── COMPONENT SEARCHING ─────────────────────────────────────────
        //

        #region COMPONENT SEARCHING

        public static bool TryGetComponentsInChildren<T>(this GameObject root, out List<T> foundComponents, bool skipNestedPrefabs = true)
            where T : Component
        {
            foundComponents = new List<T>();

            foundComponents.AddRange(root.GetComponents<T>());

            foreach (Transform child in root.transform)
            {
                if (skipNestedPrefabs)
                {
                    bool isRoot = PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject);

                    if (!isRoot)
                    {
                        if (child.gameObject.TryGetComponentsInChildren<T>(out List<T> childrenComponents))
                        {
                            foundComponents.AddRange(childrenComponents);
                        }
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            return foundComponents.Count > 0;
        }

        public static bool CheckForComponent<T>(this GameObject go, List<T> foundT) where T : Component
        {
            bool foundSometing = false;

            if (go.TryGetComponent<T>(out T component))
            {
                foundT.Add(component);
                foundSometing = true;
            }

            return foundSometing;
        }

        #endregion // COMPONENT SEARCHING

        //
        // ─── PREFAB COMPARISON ───────────────────────────────────────────
        //
        #region PREFAB COMPARISON

        // Thanks to Stas BZ on stack
        // https://stackoverflow.com/a/42551105/11890269
        public static T GetSameComponentForDuplicate<T>(GameObject original, T originalC, GameObject duplicate)
            where T : Component
        {
            // remember hierarchy
            // TODO replace with below method
            Stack<int> path = new Stack<int>();

            GameObject g = originalC.gameObject;
            while (!object.ReferenceEquals(original, g))
            {
                path.Push(g.transform.GetSiblingIndex());
                g = g.transform.parent.gameObject;
            }

            // repeat hierarchy on duplicated object
            GameObject sameGo = duplicate;
            while (path.Count > 0)
            {
                int childIndex = path.Pop();
                if (sameGo.transform.childCount == 0 || childIndex > sameGo.transform.childCount - 1 )
                {
                    Debug.Log($"Failed to find same GO of original {original} c {originalC} for duplicate {duplicate}");
                    return null;
                }
                else
                {
                    Transform kid = sameGo.transform.GetChild(childIndex);
                    sameGo = kid.gameObject;
                }
            }

            //get component index
            var cc = originalC.gameObject.GetComponents<T>();
            int componentIndex = -1;
            for (int i = 0; i < cc.Length; i++)
            {
                if (object.ReferenceEquals(originalC, cc[i]))
                {
                    componentIndex = i;
                    break;
                }
            }

            return sameGo.GetComponents<T>()[componentIndex];
        }

        public static Stack<int> GetComponentAddressInHierarchy(GameObject root, Component component)
        {
            Stack<int> path = new Stack<int>();

            GameObject g = component.gameObject;
            while (!object.ReferenceEquals(root, g))
            {
                path.Push(g.transform.GetSiblingIndex());

                if (g.transform.parent != null)
                {
                    g = g.transform.parent.gameObject;
                }
                else break;
            }

            if (!object.ReferenceEquals(root, g))
            {
                Debug.LogError($"Failed to find component {component} address in {root}");
            }

            return path;
        }

        public static GameObject GetGameObjectAtAddress(GameObject root, Stack<int> address)
        {
            // repeat hierarchy on duplicated object
            GameObject addresee = root;
            while (address.Count > 0)
            {
                int index = address.Pop();
                if (addresee.transform.childCount > index)
                {
                    addresee = addresee.transform.GetChild(index).gameObject;
                }
                else
                {
                    Debug.LogError($"Failed to find object at given address for {root.name}, {addresee.name} does not have a child at index {index}");
                }
            }
            return addresee;
        }

        //* Oki honestly I am not sure when would I use that
        public static GameObject AsOriginalPrefab(this GameObject prefabInstance)
        {
            GameObject originalPrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefabInstance);

            if (originalPrefab == null)
            {
                Debug.LogError($"{prefabInstance.name} - couldnt find original prefab");
            }

            return originalPrefab;
        }

        public static GameObject FindInstanceOfTheSamePrefab(this IEnumerable<GameObject> collection, GameObject prefabInstance)
        {
            foreach (GameObject instance in collection)
            {
                if (AreInstancesOfTheSamePrefab(instance, prefabInstance))
                {
                    return instance;
                }
            }

            return null;
        }

        public static bool AreInstancesOfTheSamePrefab(GameObject instance1, GameObject instance2)
        {
            return instance1.AsOriginalPrefab() == instance2.AsOriginalPrefab();
        }

        #endregion // PREFAB COMPARISON

        //
        // ─── SCRIPT SEARCHING ───────────────────────────────────────────────────────────
        //

        #region SCRIPT SEARCHING

        public static void FindScriptsInHierarchy(this GameObject root, out List<MonoBehaviour> foundScripts, bool skipNestedPrefabs = true)
        {
            foundScripts = new List<MonoBehaviour>();

            root.TryGetScripts(foundScripts);

            foreach (Transform child in root.transform)
            {
                if (skipNestedPrefabs)
                {
                    bool isRoot = PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject);

                    // In this case we are only interested in children which are not nested prefabs
                    if (!isRoot)
                    {
                        FindScriptsInHierarchy(child.gameObject, out List<MonoBehaviour> childScripts);
                        foundScripts.AddRange(childScripts);
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        //TODO This would have to be substituted with a list of excluded namespaces
        public static bool TryGetScripts(this GameObject go, List<MonoBehaviour> foundScripts)
        {
            bool foundSometing = false;

            go.GetComponents<MonoBehaviour>().ToList().ForEach((mono) =>
            {
                // Just to be sure, we don't want to grab original unity components
                if (mono != null)
                {
                    try
                    {
                        string monoNamespce = mono.GetType().Namespace;

                        if (monoNamespce != null)
                        {
                            if (monoNamespce.Contains("UnityEngine") == false
                            && monoNamespce.Contains("TMPro") == false)
                            {
                                foundScripts.Add(mono);
                                foundSometing = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error at trying to get scripts in {go.transform.root.name} gameobject:");
                        Debug.LogError(ex.Message + ex.StackTrace);
                    }
                }
                else
                {
                    Debug.LogError($"Mysterious null mono found at {go.transform.root.name} object");
                }
            });

            return foundSometing;
        }

        #endregion // SCRIPT SEARCHING

    }
}
