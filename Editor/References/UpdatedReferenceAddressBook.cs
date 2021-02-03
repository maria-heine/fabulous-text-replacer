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

        public void ClearAddressBook()
        {
            fakePrefabsUpdatedReferences = new List<FakeDictionary>();
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