using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FabulousReplacer
{
    public partial class FabulousTextComponentReplacer
    {
        private class TextRefernce
        {
            public Text originalPrefabText { get; private set; }
            public List<Text> textInstancesWithoutFoundReferences { get; private set; }
            public List<MonoBehaviour> localTextReferences { get; private set; }
            public Dictionary<Text, List<MonoBehaviour>> foreignTextReferencesDictionary { get; private set; }
            //public IEnumerable<Component> LocalTextReferences => localTextReferences;

            public TextRefernce(Text originalPrefabText)
            {
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
                if (localTextReferences == null)
                {
                    localTextReferences = new List<MonoBehaviour>();
                }

                localTextReferences.AddRange(localReferences);
            }

            public void AddForeignTextReference(Text textInstance, List<MonoBehaviour> references)
            {
                if (foreignTextReferencesDictionary == null)
                {
                    foreignTextReferencesDictionary = new Dictionary<Text, List<MonoBehaviour>>();
                }

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
}