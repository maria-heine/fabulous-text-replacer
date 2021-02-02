using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System;

namespace FabulousReplacer
{
    //TODO remove entirely
    [Obsolete]
    public class TextRefernce
    {
        public string prefabPath { get; private set;}

        //* Text components
        public Text originalPrefabText { get; private set; }
        public TextMeshProUGUI updatedTMProText { get; set; }

        //* References
        public List<Text> textInstancesWithoutFoundReferences { get; private set; } // ? WTF IS THAT
        public List<MonoBehaviour> localTextReferences { get; private set; }
        public Dictionary<Text, List<MonoBehaviour>> foreignTextReferencesDictionary { get; private set; }
        // Below includes both originalPrefabText and all it's instances found across the project's nested prefabs
        // public Dictionary<Text, List<MonoBehaviour>> textReferencesDictionary { get; private set; }
        // public Dictionary<Type, bool> isUpdatedUniqeMonobehaviourType { get; private set; }

        public TextRefernce(string prefabPath, Text originalPrefabText)
        {
            // PrefabUtility.
            this.prefabPath = prefabPath;
            this.originalPrefabText = originalPrefabText;
        }

        // ! This could also just mean reference not found
        public void AddUnreferencedTextInstance(Text textInstance)
        {
            if (textInstancesWithoutFoundReferences == null)
            {
                textInstancesWithoutFoundReferences = new List<Text>();
            }
            textInstancesWithoutFoundReferences.Add(textInstance);
        }

        public void SetLocalTextReferences(List<MonoBehaviour> localReferences)
        {
            localTextReferences = localReferences;
        }

        public void AddLocalTextReference(List<MonoBehaviour> localReferences)
        {
            if (localTextReferences == null) localTextReferences = new List<MonoBehaviour>();
            // if (isUpdatedUniqeMonobehaviourType == null) isUpdatedUniqeMonobehaviourType = new Dictionary<Type, bool>();

            localTextReferences.AddRange(localReferences);
            // SaveUniqueMonoBehaviourTypes(localReferences);
        }

        public void AddForeignTextReference(Text textInstance, List<MonoBehaviour> references)
        {
            if (foreignTextReferencesDictionary == null) foreignTextReferencesDictionary = new Dictionary<Text, List<MonoBehaviour>>();
            // if (isUpdatedUniqeMonobehaviourType == null) isUpdatedUniqeMonobehaviourType = new Dictionary<Type, bool>();

            if (!foreignTextReferencesDictionary.ContainsKey(textInstance))
            {
                foreignTextReferencesDictionary.Add(textInstance, references);
            }
            else
            {
                foreignTextReferencesDictionary[textInstance].AddRange(references);
            }

        }
    }
}