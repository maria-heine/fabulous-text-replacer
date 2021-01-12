using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FabulousReplacer
{
    public static class FabulousExtensions
    {
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

        public static GameObject AsOriginalPrefab(this GameObject prefabInstance)
        {
            GameObject originalPrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefabInstance);
            
            if (originalPrefab == null)
            {
                Debug.LogError($"{prefabInstance.name} - couldnt find original prefab");
            }
            
            return PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefabInstance);
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

        public static void CheckHierarchyForNestedPrefabs(this GameObject root, List<GameObject> foundNestedPrefabs)
        {
            foreach (Transform child in root.transform)
            {
                bool isRoot = PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject);

                if (isRoot)
                {
                    foundNestedPrefabs.Add(child.gameObject.AsOriginalPrefab());
                }
                else if (child.childCount > 0)
                {
                    CheckHierarchyForNestedPrefabs(child.gameObject, foundNestedPrefabs);
                }
            }
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
