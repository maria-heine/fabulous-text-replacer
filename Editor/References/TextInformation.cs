using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace FabulousReplacer
{
    public class TextInformation
    {
        public GameObject Parent { get; set; }
        public string Text { get; set; }
        public TextAnchor Alignment { private get; set; }
        public int FontSize { get; set; }
        public Color FontColor { get; set; }
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
        }
    }
}
