using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static FabulousReplacer.FabulousExtensions;

namespace FabulousReplacer
{
    /*
    It was important to know that the 
    https://docs.unity3d.com/Manual/script-Serialization.html
    */
    [System.Serializable]
    public class UpdatedReference
    {
        // public string prefabPath;
        public Type monoType;
        public string fieldName;
        public Stack<int> monoAddress;
        public Stack<int> referencedTextAddress;

        public GameObject originalPrefab;
        public Text originalText;

        public void SaveMonoBehaviourAddress(GameObject prefab, MonoBehaviour mono)
        {
            monoAddress = GetComponentAddressInHierarchy(prefab, mono);
        }

        public void SaveReferencedTextAddress(GameObject prefab, Text text)
        {
            referencedTextAddress = GetComponentAddressInHierarchy(prefab, text);
        }
    }
}