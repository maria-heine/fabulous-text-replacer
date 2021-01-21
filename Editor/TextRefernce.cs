using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FabulousReplacer
{
    public partial class FabulousTextComponentReplacer
    {
        private class TextRefernce
        {
            Text originalPrefabText;
            List<Text> textInstancesWithoutFoundReferences;
            List<Component> localTextReferences;
            Dictionary<Text, List<Component>> foreignTextReferencesDictionary;

            public TextRefernce(Text originalPrefabText)
            {
                this.originalPrefabText = originalPrefabText;
            }

            public void AddTextInstance(Text textInstance)
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