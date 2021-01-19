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
                textInstancesWithoutFoundReferences ??= new List<Text>();
                textInstancesWithoutFoundReferences.Add(textInstance);
            }

            public void SetLocalTextReferences(List<Component> localReferences)
            {
                localTextReferences = localReferences;
            }

            public void AddLocalTextReference(List<Component> localReferences)
            {
                localTextReferences ??= new List<Component>();
                localTextReferences.AddRange(localReferences);
            }

            public void AddForeignTextReference(Text textInstance, List<Component> references)
            {
                foreignTextReferencesDictionary ??= new Dictionary<Text, List<Component>>();
                foreignTextReferencesDictionary.Add(textInstance, references);
            }
        }
    }
}