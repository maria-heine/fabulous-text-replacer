using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FabulousReplacer
{
    [CreateAssetMenu(fileName = "UpdatedReferenceAddressBook", menuName = "FabulousReplacer/UpdatedReferenceAddressBook")]
    [System.Serializable]
    public class UpdatedReferenceAddressBook : ScriptableObject, IEnumerable<KeyValuePair<string, List<UpdatedReference>>>
    {
        //TODO Remove
        public int counter;
        public List<UpdatedReference> updatedReferencesPreview;

        // * Stupid Unity still doesnt serialize dictionaries 
        [System.Serializable]
        private class FakeDictionary
        {
            public string prefabPath;
            public List<UpdatedReference> updatedReferences;

            public FakeDictionary(string prefabPath)
            {
                this.prefabPath = prefabPath;
                updatedReferences = new List<UpdatedReference>();
            }
        }
        
        private List<FakeDictionary> fakePrefabsUpdatedReferences;
        // public Dictionary<string, List<UpdatedReference>> prefabsUpdatedReferences;

        public List<UpdatedReference> this[string prefabPath]
        {
            get
            {
                counter++;

                FakeDictionary entry = fakePrefabsUpdatedReferences.Find(entry => entry.prefabPath == prefabPath);

                if (entry == null)
                {
                    string guid = AssetDatabase.AssetPathToGUID(prefabPath);
                    if (guid == null)
                    {
                        Debug.LogError($"Incorrect asset path: {prefabPath}");
                    }

                    entry = new FakeDictionary(prefabPath);
                    fakePrefabsUpdatedReferences.Add(entry);
                    return entry.updatedReferences;
                }
                else
                {
                    return entry.updatedReferences;
                }
            }
        }

        // public List<UpdatedReference> this[string prefabPath]
        // {
        //     get
        //     {
        //         if (!prefabsUpdatedReferences.ContainsKey(prefabPath))
        //         {
        //             string guid = AssetDatabase.AssetPathToGUID(prefabPath);
        //             if (guid == null)
        //             {
        //                 Debug.LogError($"Incorrect asset path: {prefabPath}");
        //             }

        //             prefabsUpdatedReferences[prefabPath] = new List<UpdatedReference>();
        //         }
        //         counter++;
        //         return prefabsUpdatedReferences[prefabPath];
        //     }
        // }

        public void ClearAddressBook()
        {
            // prefabsUpdatedReferences = new Dictionary<string, List<UpdatedReference>>();
            fakePrefabsUpdatedReferences = new List<FakeDictionary>();
            updatedReferencesPreview = new List<UpdatedReference>();
        }

        // public void AddPrefabReference(string prefabPath, UpdatedReference updatedReference)
        // {
        //     if (prefabsUpdatedReferences == null) prefabsUpdatedReferences = new Dictionary<string, List<UpdatedReference>>();
        //     if (!prefabsUpdatedReferences.ContainsKey(prefabPath))
        //     {
        //         prefabsUpdatedReferences[prefabPath] = new List<UpdatedReference>();
        //     }
        //     prefabsUpdatedReferences[prefabPath].Add(updatedReference);
        //     updatedReferencesPreview.Add(updatedReference);
        // }

        //
        // ─── ENUMERATOR ──────────────────────────────────────────────────
        //

        // public IEnumerator<KeyValuePair<string, List<UpdatedReference>>> GetEnumerator()
        public IEnumerator<KeyValuePair<string, List<UpdatedReference>>> GetEnumerator()
        {
            // Debug.Log(prefabsUpdatedReferences);
            foreach (FakeDictionary entry in fakePrefabsUpdatedReferences)
            {
                string key = entry.prefabPath;        
                List<UpdatedReference> value = entry.updatedReferences;           
                yield return new KeyValuePair<string, List<UpdatedReference>>(key, value);
            }
            
            // return prefabsUpdatedReferences.GetEnumerator();
            // return fakePrefabsUpdatedReferences.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}