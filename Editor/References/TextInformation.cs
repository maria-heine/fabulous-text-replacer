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
        public float LineSpacing;
        public Font Font;
        public FontStyle FontStyle;
        public TextAnchor Alignment;
        public bool Wrapping;

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

        public FontStyles TMProFontStyle
        {
            get
            {
                switch (FontStyle)
                {
                    case FontStyle.Bold:
                        return FontStyles.Bold;
                    case FontStyle.BoldAndItalic:
                        return FontStyles.Bold | FontStyles.Italic;
                    case FontStyle.Italic:
                        return FontStyles.Italic;
                    case FontStyle.Normal:
                        return FontStyles.Normal;
                    default:
                        return FontStyles.Normal;
                }
            }
        }

        public TextInformation(Text text)
        {
            Parent = text.gameObject;
            Text = text.text;
            Alignment = text.alignment;
            Font = text.font;
            FontSize = text.fontSize;
            FontColor = text.color;
            FontStyle = text.fontStyle;
            AutoSize = text.resizeTextForBestFit;
            MaxSize = text.resizeTextMaxSize;
            MinSize = text.resizeTextMinSize;
            IsRichText = text.supportRichText;
            LineSpacing = text.lineSpacing;
            Wrapping = text.horizontalOverflow == HorizontalWrapMode.Wrap;
        }

        public void StyleTMProText(TextMeshProUGUI tmProText, FontAssetMap fontAssetMap)
        {
            tmProText.text = Text;
            tmProText.alignment = TMProAlignment;
            tmProText.font = fontAssetMap.GetNewFont(Font);
            tmProText.fontSize = (float)FontSize;
            tmProText.fontStyle = TMProFontStyle;
            tmProText.color = FontColor;
            tmProText.richText = true;
            tmProText.characterSpacing = 0f;
            tmProText.lineSpacing = LineSpacing;
            tmProText.enableWordWrapping = Wrapping;
            tmProText.enableAutoSizing = AutoSize;
            tmProText.fontSizeMax = MaxSize;
            tmProText.fontSizeMin = MinSize;
        }
    }
}
