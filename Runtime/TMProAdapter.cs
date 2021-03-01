using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

//! This is a very rough approximation of adapter pattern since it shouldnt be used directly as Text replacement
// It should be passed to other referers through it's base Text class
// But Unity doesn't allow overrides on gameobject and transform etc.
public class TMProAdapter : Text
{
    [SerializeField] TextMeshProUGUI _textMesh;
    [SerializeField] string _fieldName;

    public string AdapterFieldName => _fieldName;
    public TextMeshProUGUI TMProText => _textMesh;
    public new GameObject gameObject => _textMesh.gameObject;
    public new Transform transform => _textMesh.transform;

    public override string text
    {
        get => _textMesh.text;
        set => _textMesh.text = value;
    }

    public override Color color
    {
        get => _textMesh.color;
        set => _textMesh.color = value;
    }

    public new bool enabled
    {
        get => _textMesh.enabled;
        set => _textMesh.enabled = value;
    }

    private new void Awake()
    {
        base.enabled = false;
        base.raycastTarget = false;
        base.Awake();
    }

    public void SetupAdapter(string fieldName, TextMeshProUGUI textMesh)
    {
        _textMesh = textMesh;
        _fieldName = fieldName;
    }
}
