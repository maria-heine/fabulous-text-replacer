using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FabulousReplacer
{
    public static class FabulousExtensions
    {

        // todo problem is here wwith how we find GetComponentsInChildren
        // todo consider replacing the original stucture of _scriptsByPRefab with their instances
        // Note typesToCheck should be monobehaviours
        public static void ExtractTextReferences(this GameObject prefab, Text text, IEnumerable<Type> typesToCheck, out List<Component> textReferences)
        {
            textReferences = new List<Component>();
            
            foreach (Type monoType in typesToCheck)
            {
                Component[] components = prefab.GetComponentsInChildren(monoType, true);

                foreach (Component component in components)
                {
                    if (component.IsReferencingComponent(text))
                    {
                        textReferences.Add(component);
                    }
                }
            }
        }

        // Thanks to Stas BZ on stack
        // https://stackoverflow.com/a/42551105/11890269
        public static T GetSameComponentForDuplicate<T>(GameObject original, T originalC, GameObject duplicate)
            where T : Component
        {
            // remember hierarchy
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
                sameGo = sameGo.transform.GetChild(path.Pop()).gameObject;
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

        public static bool TryGetScripts(this GameObject go, List<MonoBehaviour> foundScripts)
        {
            bool foundSometing = false;

            go.GetComponents<MonoBehaviour>().ToList().ForEach((mono) =>
            {
                // Just to be sure, we don't want to grab original unity components
                if (mono.GetType().Namespace.Contains("UnityEngine") == false)
                {
                    foundScripts.Add(mono);
                    foundSometing = true;
                }
            });

            return foundSometing;
        }

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

        public static void FindScriptsInHierarchy(this GameObject root, List<MonoBehaviour> foundScripts)
        {
            root.TryGetScripts(foundScripts);

            foreach (Transform child in root.transform)
            {
                bool isRoot = PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject);

                // In this case we are only interested in children which are not nested prefabs
                if (!isRoot)
                {
                    FindScriptsInHierarchy(child.gameObject, foundScripts);
                }
            }
        }
    }
}
