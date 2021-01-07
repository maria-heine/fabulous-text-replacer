using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TextToTextMeshProAdapter : Text
{
    private TextMeshProUGUI _textMesh;

    public void SetTextMeshProAdapterReference(TextMeshProUGUI textMesh)
    {
        _textMesh = textMesh;
    }

    public override string text 
    { 
        get => _textMesh.text; 
        set => _textMesh.text = value; 
    }
}
