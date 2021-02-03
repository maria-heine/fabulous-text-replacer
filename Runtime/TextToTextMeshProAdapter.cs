using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TextToTextMeshProAdapter : Text
{
    private TextMeshProUGUI _textMesh;

    public TextToTextMeshProAdapter() { }

    public TextToTextMeshProAdapter(TextMeshProUGUI textMesh)
    {
        if (textMesh == null)
        {
            Debug.LogError($"Text mesh reference is null for {textMesh.gameObject.name} at {textMesh.transform.root.name}");
        }

        _textMesh = textMesh;
    }

    public new GameObject gameObject => _textMesh.gameObject;
    public new Transform transform => _textMesh.transform;

    // public void SetTextMeshProAdapterReference(TextMeshProUGUI textMesh)
    // {
    //     _textMesh = textMesh;
    // }

    public override string text 
    { 
        get => _textMesh.text; 
        set => _textMesh.text = value; 
    }
}
