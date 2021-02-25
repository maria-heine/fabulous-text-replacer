using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FabulousReplacer
{
    [CreateAssetMenu(fileName = "UpdatedReferenceAddressBook", menuName = "FabulousReplacer/UpdatedReferenceAddressBook")]
    [System.Serializable]
    public class UpdatedReferenceAddressBook : ScriptableObject, IEnumerable<KeyValuePair<string, List<ReplaceUnit>>>
    {
        // * Stoopid Unity still doesnt serialize dictionaries 
        [System.Serializable]
        private class FakeDictionary
        {
            public string prefabPath;
            public List<ReplaceUnit> updatedReferences;

            public FakeDictionary(string prefabPath)
            {
                this.prefabPath = prefabPath;
                updatedReferences = new List<ReplaceUnit>();
            }
        }
        
        [SerializeField] List<FakeDictionary> fakePrefabsUpdatedReferences;
        [SerializeField] public List<string> _allFoundMonoBehaviourTypeNames;

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

        public List<ReplaceUnit> this[string prefabPath]
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

        public IEnumerable<ReplaceUnit> this[int i]
        {
            get
            {
                List<ReplaceUnit> references = new List<ReplaceUnit>();
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
        public IEnumerator<KeyValuePair<string, List<ReplaceUnit>>> GetEnumerator()
        {
            foreach (FakeDictionary entry in fakePrefabsUpdatedReferences)
            {
                string key = entry.prefabPath;        
                List<ReplaceUnit> value = entry.updatedReferences;           
                yield return new KeyValuePair<string, List<ReplaceUnit>>(key, value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}