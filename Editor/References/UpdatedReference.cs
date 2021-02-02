using System;
using System.Collections.Generic;
using UnityEditor;
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
        [SerializeField] string rootPrefabName; // just for inspector display puroses
        public bool isReferenced;
        public GameObject rootPrefab; 
        public string prefabPath;
        public string fieldName;
        public string monoAssemblyName;
        public Text originalText;
        public TextInformation textInformation;

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
        public Stack<int> TextAddress
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

        public UpdatedReference(GameObject rootPrefab, Text unreferencedText)
        {
            rootPrefabName = rootPrefab.gameObject.name;
            textInformation = new TextInformation(unreferencedText);
            prefabPath = AssetDatabase.GetAssetPath(rootPrefab);
            this.rootPrefab = rootPrefab;
            this.originalText = unreferencedText;
            TextAddress = GetComponentAddressInHierarchy(rootPrefab, unreferencedText);
            isReferenced = false;
        }

        public UpdatedReference(GameObject referencingPrefab, Text referencedText, MonoBehaviour referencingMono, string fieldName)
        {
            rootPrefabName = referencingPrefab.gameObject.name;
            prefabPath = AssetDatabase.GetAssetPath(referencingPrefab);
            textInformation = new TextInformation(referencedText);
            this.rootPrefab = referencingPrefab;
            this.originalText = referencedText;
            this.MonoType = referencingMono.GetType();
            this.fieldName = fieldName;
            MonoAddress = GetComponentAddressInHierarchy(referencingPrefab, referencingMono);
            TextAddress = GetComponentAddressInHierarchy(referencingPrefab, referencedText);
            isReferenced = true;
        }
    }
}