using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FabulousReplacer
{
    [CreateAssetMenu(fileName = "UpdatedReferenceAddressBook", menuName = "FabulousReplacer/UpdatedReferenceAddressBook")]
    [System.Serializable]
    public class UpdatedReferenceAddressBook : ScriptableObject, IEnumerable<KeyValuePair<string, List<UpdatedReference>>>
    {
        // * Stoopid Unity still doesnt serialize dictionaries 
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
        
        [SerializeField] List<FakeDictionary> fakePrefabsUpdatedReferences;

        private string[] paths;
        public string[] Paths
        {
            get
            {
                if (paths == null || paths.Length == 0)
                {
                    paths = fakePrefabsUpdatedReferences.Select(x => x.prefabPath).Distinct().ToArray();
                }

                return paths;
            }
        }

        public int Count => Paths.Length; 

        public List<UpdatedReference> this[string prefabPath]
        {
            get
            {
                FakeDictionary entry = fakePrefabsUpdatedReferences.Find(item => item.prefabPath == prefabPath);

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

        public IEnumerable<UpdatedReference> this[int i]
        {
            get
            {
                List<UpdatedReference> references = new List<UpdatedReference>();
               return fakePrefabsUpdatedReferences
                    .Where(y => y.prefabPath == Paths[i])
                    .SelectMany(x => x.updatedReferences);
            }
        }

        public void ClearAddressBook()
        {
            fakePrefabsUpdatedReferences = new List<FakeDictionary>();
            paths = null;
        }

        //
        // ─── ENUMERATOR ──────────────────────────────────────────────────
        //

        // public IEnumerator<KeyValuePair<string, List<UpdatedReference>>> GetEnumerator()
        public IEnumerator<KeyValuePair<string, List<UpdatedReference>>> GetEnumerator()
        {
            foreach (FakeDictionary entry in fakePrefabsUpdatedReferences)
            {
                string key = entry.prefabPath;        
                List<UpdatedReference> value = entry.updatedReferences;           
                yield return new KeyValuePair<string, List<UpdatedReference>>(key, value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}