using PDFDict.SDK.Sharp.Core.Contents;
using System.Drawing;

namespace img2table.sharp.web.Services
{
    public class ContentElement
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
        public PageElement PageElement { get; set; }

        public RectangleF Rect()
        {
            return RectangleF.FromLTRB(Left, Top, Right, Bottom);
        }

        public string OCRText { get; set; } = string.Empty;

        public string Content
        {
            get
            {
                if (PageElement is TextElement textElement)
                {
                    return textElement.GetText(true);
                }
                else if (PageElement is ImageElement)
                {
                    return OCRText;
                }

                return string.Empty;
            }
        }
    }
}
