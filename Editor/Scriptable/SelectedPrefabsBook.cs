using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SelectedPrefabsBook", menuName = "FabulousReplacer/SelectedPrefabsBook")]
[System.Serializable]
public class SelectedPrefabsBook : ScriptableObject
{
    public List<GameObject> SelectedPrefabs;
}
