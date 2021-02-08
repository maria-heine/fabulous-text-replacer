using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TMProAdapter : Text
{
    [SerializeField] TextMeshProUGUI _textMesh;
    [SerializeField] string _fieldName;

    public string AdapterFieldName => _fieldName;
    public TextMeshProUGUI TMProText => _textMesh;

    public void SetupAdapter(string fieldName, TextMeshProUGUI textMesh)
    {
        _textMesh = textMesh;
        _fieldName = fieldName;
    }

    public new GameObject gameObject => _textMesh.gameObject;
    public new Transform transform => _textMesh.transform;

    // public void SetTextMeshProAdapterReference(TextMeshProUGUI textMesh)
    // {
    //     _textMesh = textMesh;
    // }

    public override string text 
    { 
        get
        {
            string re = null;

            try
            {
                return _textMesh.text;
            }
            catch
            {
                Debug.Log($"oops {transform.gameObject.name}", transform);
            }

            return re;
        } 
        set => _textMesh.text = value; 
    }
}
