using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class TMProAdapterParent : MonoBehaviour
{
    public TMProAdapter this[string fieldName]
    {
        get
        {
            return GetComponentsInChildren<TMProAdapter>().First(x => x.AdapterFieldName == fieldName);
        }
    }
}
