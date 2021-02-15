using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public class TMProAdapterParent : MonoBehaviour
{
    //TODO Add version using FieldInformation instead
    public TMProAdapter this[string fieldName]
    {
        get
        {
            return GetComponentsInChildren<TMProAdapter>().First(x => x.AdapterFieldName == fieldName);
        }
    }
}
