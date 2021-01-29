using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static FabulousReplacer.FabulousExtensions;

namespace FabulousReplacer
{
    /*
    It was important to know that the:
    https://docs.unity3d.com/Manual/script-Serialization.html
    https://docs.unity3d.com/ScriptReference/SerializeField.html
    Unity cannot serialize Dictionaries, Stacks, only primitive stuff and their own classes
    oof

    Also keeping direct references to the monobehaviour and text components here instead of
    using that weird "address" thing is illegal aswell. This is because after the scripts
    are edited and reimported these references would point to some arbitrary "floating objects" 
    that are no longer directly connected to the prefabs they were created from. 
    */
    [System.Serializable]
    public class UpdatedReference
    {

        // todo solve the problem of one to many references for a text and monobehaviours
        // public string prefabPath;
        [SerializeField] string referencingPrefabName; // just for inspector display puroses
        public GameObject originalPrefab; //! rename to "referencingPrefab"?
        public string fieldName;
        public string monoAssemblyName;
        public Text originalText;

        [SerializeField] List<int> monoAddress;
        public Stack<int> MonoAddress
        {
            get
            {
                Stack<int> stack = new Stack<int>(monoAddress.Count);
                foreach (int i in monoAddress)
                {
                    stack.Push(i);
                }
                return stack;
            }
            set
            {
                monoAddress = new List<int>(value.Count);
                while (value.Count > 0)
                {
                    monoAddress.Insert(0, value.Pop());
                }
            }
        }

        [SerializeField] List<int> referencedTextAddress;
        public Stack<int> ReferencedTextAddress
        {
            get
            {
                Stack<int> stack = new Stack<int>(referencedTextAddress.Count);
                foreach (int i in referencedTextAddress)
                {
                    stack.Push(i);
                }
                return stack;
            }
            set
            {
                referencedTextAddress = new List<int>(value.Count);
                while (value.Count > 0)
                {
                    referencedTextAddress.Insert(0, value.Pop());
                }
            }
        }

        [SerializeField] Type monoType;
        public Type MonoType
        {
            get
            {
                if (monoType == null) Debug.LogError($"{monoAssemblyName} Type is already null");
                return monoType;
            }
            set
            {
                if (value == null)
                {
                    Debug.LogError($"What are u even doing, keeping: {monoAssemblyName}.");
                }
                else
                {
                    monoAssemblyName = value.AssemblyQualifiedName;
                    monoType = value;
                }
            }
        }

        public UpdatedReference(GameObject parentPrefab, Text referencedText, MonoBehaviour referencingMono, string fieldName)
        {
            referencingPrefabName = parentPrefab.gameObject.name;
            this.originalPrefab = parentPrefab;
            this.originalText = referencedText;
            this.MonoType = referencingMono.GetType();
            this.fieldName = fieldName;
            MonoAddress = GetComponentAddressInHierarchy(parentPrefab, referencingMono);
            ReferencedTextAddress = GetComponentAddressInHierarchy(parentPrefab, referencedText);
        }
    }
}