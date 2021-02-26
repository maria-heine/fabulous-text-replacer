using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "FontAssetMap", menuName = "FabulousReplacer/FontAssetMap")]
public class FontAssetMap : ScriptableObject
{
    [SerializeField] private TMP_FontAsset defaultFont;
    [SerializeField] private List<FontAssetReplacePair> fontAssetReplacePairs;

    public TMP_FontAsset GetNewFont(Font oldFont)
    {
        FontAssetReplacePair fontPair = fontAssetReplacePairs.Find(fp => fp.oldFont == oldFont);
        return fontPair != null ? fontPair.newTMProFont : defaultFont;
    }

    [System.Serializable]
    private class FontAssetReplacePair
    {
        public Font oldFont;
        public TMP_FontAsset newTMProFont;
    }
}
