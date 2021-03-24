using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(TMProAdapter))]
public class TextToTextMeshProAdapterEditor : Editor
{
    TMProAdapter _adapter;

    public void OnEnable()
    {
        _adapter = (TMProAdapter)target;
    }

    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = new VisualElement();

        // Label label = new Label($"Text adapter for the {_adapter.}")

        TextField fieldName = new TextField("Field name")
        {
            value = _adapter.AdapterFieldName
        };
        ObjectField tmProField = new ObjectField("TMPro Component")
        {
            objectType = typeof(TextMeshProUGUI),
            value = _adapter.TMProText
        };

        root.Add(fieldName);
        root.Add(tmProField);

        return root;
    }
}
