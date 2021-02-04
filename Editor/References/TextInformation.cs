using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace FabulousReplacer
{
    [System.Serializable]
    public class TextInformation
    {
        public GameObject Parent;
        public string Text;
        public int FontSize;
        public Color FontColor;
        public bool AutoSize;
        public int MaxSize;
        public int MinSize;
        public bool IsRichText;
        
        public TextAnchor Alignment { private get; set; }
        public TextAlignmentOptions TMProAlignment
        {
            get
            {
                switch (Alignment)
                {
                    case TextAnchor.LowerCenter:
                        return TextAlignmentOptions.Bottom;
                    case TextAnchor.LowerLeft:
                        return TextAlignmentOptions.BottomLeft;
                    case TextAnchor.LowerRight:
                        return TextAlignmentOptions.BottomRight;
                    case TextAnchor.MiddleCenter:
                        return TextAlignmentOptions.Midline;
                    case TextAnchor.MiddleLeft:
                        return TextAlignmentOptions.MidlineLeft;
                    case TextAnchor.MiddleRight:
                        return TextAlignmentOptions.MidlineRight;
                    case TextAnchor.UpperCenter:
                        return TextAlignmentOptions.Top;
                    case TextAnchor.UpperLeft:
                        return TextAlignmentOptions.TopLeft;
                    case TextAnchor.UpperRight:
                        return TextAlignmentOptions.TopRight;
                    default:
                        return TextAlignmentOptions.Midline;
                }
            }
        }

        public TextInformation(Text text)
        {
            Parent = text.gameObject;
            Text = text.text;
            Alignment = text.alignment;
            FontSize = text.fontSize;
            FontColor = text.color;
            AutoSize = text.resizeTextForBestFit;
            MaxSize = text.resizeTextMaxSize;
            MinSize = text.resizeTextMinSize;
            IsRichText = text.supportRichText;
        }
    }
}
