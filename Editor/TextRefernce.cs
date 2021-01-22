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
            public List<Component> localTextReferences { get; private set; }
            public Dictionary<Text, List<Component>> foreignTextReferencesDictionary { get; private set; }
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

            public void SetLocalTextReferences(List<Component> localReferences)
            {
                localTextReferences = localReferences;
            }

            public void AddLocalTextReference(List<Component> localReferences)
            {
                if (localTextReferences == null)
                {
                    localTextReferences = new List<Component>();
                }

                localTextReferences.AddRange(localReferences);
            }

            public void AddForeignTextReference(Text textInstance, List<Component> references)
            {
                if (foreignTextReferencesDictionary == null)
                {
                    foreignTextReferencesDictionary = new Dictionary<Text, List<Component>>();
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