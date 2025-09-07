using img2table.sharp.Img2table.Sharp.Tabular;
using img2table.sharp.web.Models;
using PDFDict.SDK.Sharp.Core.Contents;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace img2table.sharp.web.Services
{
    public class LineBreakProcessor
    {
        public class TextParagraph
        {
            public bool IsListItem { get; set; }
            public bool IsOrderedList { get; set; }
            public string ListTag { get; set; }
            public double AverageCharWidth { get; set; }
            public int LeftIndent { get; set; }
            public string Text { get; set; }
            public List<ContentElement> Contents { get; set; }

            public TextParagraph()
            {
                Contents = new List<ContentElement>();
            }
        }

        public static void ProcessLineBreaks(ChunkElement chunkElement, bool autoOCR, string workFolder, string pageImagePath, bool hiddenListTag)
        {
            var contents = chunkElement.ContentElements;
            ProcessLineBreaks(contents, chunkElement.ChunkObject, autoOCR, workFolder, pageImagePath, hiddenListTag, false);
        }

        public static List<TextParagraph> ProcessLineBreaks(IEnumerable<ContentElement> contents, ObjectDetectionResult chunkObject, bool autoOCR, string workFolder, 
            string pageImagePath, bool hiddenListTag, bool loadTextStyle)
        {
            if (contents == null || !contents.Any())
            {
                return new List<TextParagraph>();
            }

            var paragraphs = new List<TextParagraph>();
            var lines = GroupLine(contents.ToList());
            StringBuilder sb = new StringBuilder();
            var textParagraph = new TextParagraph();
            foreach (var line in lines)
            {
                double averageCharWidth = CalcAverageCharWidth(line);
                ContentElement lastSeg = null;
                
                for (int i = 0; i < line.Count; i++)
                {
                    var seg = line[i];

                    if (seg.PageElement is ImageElement)
                    {
                        if (autoOCR)
                        {
                            var imageName = $"img_{Guid.NewGuid().ToString()}.png";
                            string tempImagePath = Path.Combine(workFolder, imageName);
                            ChunkUtils.ClipImage(pageImagePath, tempImagePath, seg.Rect(), false);

                            seg.OCRText = OCRUtils.PaddleOCRText(tempImagePath);

                            if (string.IsNullOrEmpty(seg.OCRText))
                            {
                                continue;
                            }
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (i == 0)
                    {
                        // is symbol winding font?
                        bool isWindingSymbol = false;
                        if (seg.PageElement is TextElement)
                        {
                            isWindingSymbol = ((TextElement)seg.PageElement).IsWingdingFont();
                        }
                        // if this is standalone list tag element, calc next seg distance? is Tab?
                        bool tabAfter = false;
                        if (i < line.Count - 1)
                        {
                            var nextSeg = line[i + 1];
                            var distance = nextSeg.Left - seg.Right;
                            if (distance > averageCharWidth)
                            {
                                tabAfter = true;
                            }
                        }

                        string text = seg.Content;
                        if (TextElement.IsListParagraphBegin(seg.Content, out var ordered, out var listTag) || isWindingSymbol)
                        {
                            if (seg.Content.Trim().Length == listTag.Trim().Length && tabAfter)
                            {
                                // end last paragraph
                                if (!string.IsNullOrEmpty(textParagraph.Text) && textParagraph.Contents.Count() > 0)
                                {
                                    textParagraph.Text += "\n\n";
                                    paragraphs.Add(textParagraph);
                                    textParagraph = new TextParagraph();
                                }

                                textParagraph.IsListItem = true;
                                textParagraph.ListTag = listTag;
                                textParagraph.IsOrderedList = ordered;
                                textParagraph.LeftIndent = seg.Left;
                                textParagraph.AverageCharWidth = averageCharWidth;

                                if (hiddenListTag && listTag != null)
                                {
                                    text = text.Remove(0, listTag.Length);
                                }
                            }
                        }

                        if (loadTextStyle && seg.PageElement is TextElement textElement && textElement.TryBuildHTMLPiece(out var html, text))
                        {
                            sb.Append(html);
                        }
                        else
                        {
                            sb.Append(text);
                        }
                    }
                    else
                    {
                        bool insertWS = true;
                        char? lastC = sb.Length == 0? null: sb.ToString().Last();
                        char? nextC = seg.Content.Length == 0? null: seg.Content.First();
                        if (lastC != null && (char.IsWhiteSpace(lastC.Value) || IsHyphen(lastC.Value)))
                        {
                            insertWS = false;
                        }
                        else if (nextC != null && (IsHyphen(nextC.Value) || char.IsWhiteSpace(nextC.Value)))
                        {
                            insertWS = false;
                        }
                        else if (lastSeg != null)
                        {
                            if (seg.Left - lastSeg.Right < averageCharWidth)
                            {
                                insertWS = false;
                            }
                        }

                        if (insertWS)
                        {
                            sb.Append(" ");
                        }

                        if (loadTextStyle && seg.PageElement is TextElement textElement && textElement.TryBuildHTMLPiece(out var html))
                        {
                            sb.Append(html);
                        }
                        else
                        {
                            sb.Append(seg.Content);
                        }
                    }
                }

                if (IsParagraphEnd(sb.ToString())) // || IsParagraphEnd(line, chunkObject.Width)
                {
                    sb.Append("\n\n");
                    
                    textParagraph.Text += sb.ToString();
                    textParagraph.Contents.AddRange(line);
                    paragraphs.Add(textParagraph);

                    sb.Clear();
                    textParagraph = new TextParagraph();
                }
                else
                {
                    sb.Append(" ");
                    textParagraph.Text += sb.ToString();
                    textParagraph.Contents.AddRange(line);

                    sb.Clear();
                }
            }

            if (!string.IsNullOrEmpty(textParagraph.Text) && textParagraph.Contents.Count() > 0)
            {
                sb.Append("\n\n");
                textParagraph.Text += sb.ToString();
                paragraphs.Add(textParagraph);
            }
            
            return paragraphs;
        }

        private static double CalcAverageCharWidth(IEnumerable<ContentElement> contents)
        {
            double totalWidth = 0;
            int charCount = 0;
            foreach (var c in contents)
            {
                if (string.IsNullOrEmpty(c.Content))
                {
                    continue;
                }
                charCount += c.Content.Length;
                totalWidth += c.Rect().Width;
            }
            if (charCount == 0)
            {
                return 10;
            }
            return totalWidth / charCount;
        }

        private static List<List<ContentElement>> GroupLine(List<ContentElement> contents)
        {
            var lines = new List<List<ContentElement>>();
            if (contents.Count == 0)
            {
                return lines;
            }
            if (contents.Count == 1)
            {
                lines.Add(contents);
                return lines;
            }

            lines = GroupLineByBBox(contents);
            return lines;
        }

        private static List<List<ContentElement>> GroupLineByBBox(List<ContentElement> contents)
        {
            var lines = new List<List<ContentElement>>();
            if (contents.Count == 0)
            {
                return lines;
            }
            if (contents.Count == 1)
            {
                lines.Add(contents);
                return lines;
            }

            var copy = contents.ToList();
            int top = copy.Min(c => Math.Min(c.Top, c.Bottom));
            int bottom = copy.Max(c => Math.Max(c.Top, c.Bottom));

            var line = new List<ContentElement>();
            for (int i = top + 1; i <= bottom; i++)
            {
                var temp = new List<ContentElement>();
                foreach (var cell in copy)
                {
                    if (i >= cell.Top && i <= cell.Bottom)
                    {
                        temp.Add(cell);
                    }
                }

                if (temp.Count() > 0)
                {
                    copy.RemoveAll(c => temp.Contains(c));
                    int currBottom = temp.Max(c => Math.Max(c.Top, c.Bottom));
                    foreach (var c in copy)
                    {
                        if (c.Top <= currBottom)
                        {
                            temp.Add(c);
                        }
                    }
                    copy.RemoveAll(c => temp.Contains(c));
                    line.AddRange(temp);

                    if (copy.Count() > 0)
                    {
                        i = temp.Max(c => Math.Max(c.Top, c.Bottom)) + 1;
                    }
                    else
                    {
                        line = SortLine(line);
                        lines.Add(line);
                        break;
                    }
                }
                else
                {
                    if (line.Count() > 0)
                    {
                        line = SortLine(line);
                        lines.Add(line);
                        line = new List<ContentElement>();
                    }
                }
            }

            return lines;
        }

        private static List<ContentElement> SortLine(List<ContentElement> line)
        {
            if (line == null || line.Count() == 0)
            {
                return line;
            }

            var sort = line.OrderBy(c => c.Left).ToList();
            ContentElement prev = null;
            for (int i = 0; i < sort.Count(); i++)
            {
                var curr = sort[i];
                if (prev == null)
                {
                    prev = curr;
                    continue;
                }
                if (curr.Bottom < prev.Top)
                {
                    sort[i - 1] = curr;
                    sort[i] = prev;
                }
                else
                {
                    prev = curr;
                }
            }

            return sort;
        }

        private static List<List<ContentElement>> Columnize(List<ContentElement> contents)
        {
            var cols = new List<List<ContentElement>>();
            var gaps = ScanForGapsBetweenBoxes(contents);
            if (gaps != null && gaps.Count() > 0)
            {
                foreach (var gap in gaps)
                {
                    var col = new List<ContentElement>();
                    foreach (var content in contents)
                    {
                        if (content.Right < gap)
                        {
                            col.Add(content);
                        }
                    }

                    if (col.Count > 0)
                    {
                        cols.Add(col);
                        contents.RemoveAll(c => col.Contains(c));
                    }
                }

                if (contents.Count() > 0)
                {
                    cols.Add(contents);
                }

                return cols;
            }
            else
            {
                cols.Add(contents);
                return cols;
            }
        }

        private static double EstSpaceWidth(IEnumerable<ContentElement> contents)
        {
            double defaultSpaceWidth = 10;
            if (contents == null || contents.Count() == 0)
            {
                return defaultSpaceWidth;
            }

            double totalWidth = 0;
            int charCount = 0;
            foreach (var c in contents)
            {
                if (string.IsNullOrEmpty(c.Content))
                {
                    continue;
                }
                charCount += c.Content.Length;
                totalWidth += c.Rect().Width;
            }

            if (charCount == 0)
            {
                return defaultSpaceWidth;
            }

            return totalWidth / charCount;
        }

        private static List<int> ScanForGapsBetweenBoxes(IEnumerable<ContentElement> contents)
        {
            var gaps = new List<int>();
            if (contents == null || contents.Count() == 0)
            {
                return gaps;
            }

            int step = 1;
            var minGapW = EstSpaceWidth(contents) * 2;

            var textBoxesCopy = new List<ContentElement>(contents);
            int minX = textBoxesCopy.Min(r => r.Left);
            int maxX = textBoxesCopy.Max(r => r.Right);

            int currentX = minX + 1;
            while (currentX < maxX)
            {
                if (TryGetIntersectingBox(currentX, textBoxesCopy, out var intersectingBox))
                {
                    currentX = (int)(intersectingBox.Right + 0.5) + 1;
                    textBoxesCopy.RemoveAll(box => box.Right <= currentX);
                }
                else
                {
                    int gapStart = currentX;

                    while (currentX < maxX)
                    {
                        if (TryGetIntersectingBox(currentX, textBoxesCopy, out intersectingBox))
                        {
                            break;
                        }
                        else
                        {
                            currentX += step;
                        }
                    }

                    if (currentX == maxX - 1)
                    {
                        break;
                    }

                    int gapEnd = currentX - 1;
                    int gapWidth = gapEnd - gapStart + 1;
                    if (gapWidth >= minGapW)
                    {
                        int gapCenter = (gapStart + gapEnd) / 2;
                        gaps.Add(gapCenter);
                    }

                    currentX = (int)(intersectingBox.Right + 0.5) + 1;
                    textBoxesCopy.RemoveAll(box => box.Right <= currentX);
                }
            }

            return gaps;
        }

        private static bool TryGetIntersectingBox(int x, List<ContentElement> textBoxes, out RectangleF intersectBox)
        {
            intersectBox = default;
            foreach (var box in textBoxes)
            {
                if (x >= box.Left && x <= box.Right)
                {
                    intersectBox = box.Rect();
                    return true;
                }
            }
            return false;
        }

        private static bool IsParagraphEnd(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var c = text.Trim().Last();
            if (IsPeriod(c))
            {
                return true;
            }

            if (IsHyphen(c))
            {
                return false;
            }

            // TODO

            return false;
        }

        private static bool IsParagraphEnd(List<ContentElement> line, double maxWidth)
        {
            int l = line.Min(c => c.Left);
            int r = line.Max(c => c.Right);
            var lineW = r - l;

            if (lineW / maxWidth < 2 / 3.0)
            {
                return true;
            }

            return false;
        }

        public static readonly char[] periods = { '.', '。', '．', '｡' };
        public static bool IsPeriod(char c)
        {
            return periods.Contains(c);
        }

        public static readonly char[] hyphens = {
            '-',  // HYPHEN-MINUS (U+002D)
            '‐',  // HYPHEN (U+2010)
            '‑',  // NON-BREAKING HYPHEN (U+2011)
            '‒',  // FIGURE DASH (U+2012)
            '–',  // EN DASH (U+2013)
            '—',  // EM DASH (U+2014)
            '―',  // HORIZONTAL BAR (U+2015)
            '−',  // MINUS SIGN (U+2212)
            '﹣', // SMALL HYPHEN-MINUS (U+FE63)
            '－'  // FULLWIDTH HYPHEN-MINUS (U+FF0D)
        };

        public static bool IsHyphen(char c)
        {
            return hyphens.Contains(c);
        }
    }
}
