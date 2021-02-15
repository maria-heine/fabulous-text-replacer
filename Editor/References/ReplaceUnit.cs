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

    TODO Also naming for the class is terrible, more like ReplaceUnit

    * Depending whether the text component is referenced by scripts or not different fields will be null.
    * This is far from a clear code structure unfrortunately. 

    */
    [Serializable]
    public class ReplaceUnit
    {
        // todo solve the problem of one to many references for a text and monobehaviours
        [SerializeField] string rootPrefabName; // just for inspector display puroses
        [SerializeField] string monoParentName;
        [SerializeField] string originalTextContent;
        public bool isReferenced;
        public GameObject rootPrefab;
        public string prefabPath;
        public string fieldName;
        public string monoAssemblyName;
        public Text originalText;
        public TextInformation textInformation;
        public FieldInformation fieldInformation;

        //
        // ─── ADDRESSING ──────────────────────────────────────────────────
        //

        #region Addressing

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

        #endregion // Addressing

        [SerializeField] Type monoType;

        //! OBSOLETE
        [Obsolete]
        public Type MonoType
        {
            get
            {
                if (monoType == null)
                {
                    Debug.LogError($"{monoAssemblyName} Type is already null at {rootPrefabName}");
                    monoType = Type.GetType(monoAssemblyName);
                }
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

        //
        // ─── CONSTRUCTORS ────────────────────────────────────────────────
        //

        #region Constructors

        public ReplaceUnit(GameObject rootPrefab, Text unreferencedText)
        {
            SaveBaseData(rootPrefab, unreferencedText);
            isReferenced = false;
        }

        // public ReplaceUnit(GameObject referencingPrefab, Text referencedText, MonoBehaviour referencingMono, string fieldName)
        // {
        //     SaveBaseData(referencingPrefab, referencedText);
        //     isReferenced = true;

        //     monoParentName = referencingMono.gameObject.name;
        //     GetFieldReferencingType(referencingMono, fieldName);
        //     MonoAddress = GetComponentAddressInHierarchy(referencingPrefab, referencingMono);
        // }

        public ReplaceUnit(GameObject referencingPrefab, Text referencedText, MonoBehaviour referencingMono, FieldInformation fieldInformation)
        {
            SaveBaseData(referencingPrefab, referencedText);
            isReferenced = true;

            this.fieldInformation = fieldInformation;

            monoParentName = referencingMono.gameObject.name;
            //TODO Check this!
            
            //! MOVED THIS
            // GetFieldReferencingType(referencingMono, fieldInformation.TextFieldName);

            MonoAddress = GetComponentAddressInHierarchy(referencingPrefab, referencingMono);
        }

        #endregion // Constructors

        private void SaveBaseData(GameObject rootPrefab, Text originalText)
        {
            rootPrefabName = rootPrefab.gameObject.name;
            prefabPath = AssetDatabase.GetAssetPath(rootPrefab);
            textInformation = new TextInformation(originalText);
            this.rootPrefab = rootPrefab;
            this.originalText = originalText;
            this.originalTextContent = originalText.text;
            TextAddress = GetComponentAddressInHierarchy(rootPrefab, originalText);
        }

        //! THAT WILL BE GLITCHED TOO
        private void GetFieldReferencingType(MonoBehaviour mono, string fieldName)
        {
            this.MonoType = GetFieldDeclaringType(mono.GetType(), fieldName);
            this.fieldName = fieldName;
        }
    }
}