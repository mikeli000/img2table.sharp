using Img2table.Sharp.Tabular.TableImage.TableElement;
using OpenCvSharp;
using System.Drawing;
using PDFDict.SDK.Sharp.Core.Contents;
using img2table.sharp.Img2table.Sharp.Tabular;
using img2table.sharp.Img2table.Sharp.Tabular.TableImage;

namespace Img2table.Sharp.Tabular
{
    public class ImageTabular
    {
        public static string Image2Table_WorkFolder = Path.Combine(Path.GetTempPath(), "img2table");
        private TabularParameter _parameter;

        public ImageTabular(TabularParameter tabularParameter) 
        {
            _parameter = tabularParameter;
            if (!Directory.Exists(Image2Table_WorkFolder))
            {
                Directory.CreateDirectory(Image2Table_WorkFolder);
            }
        }

        public PagedTable Process(string imgFile, RectangleF? tableBbox = null, IEnumerable<TextRect> textBoxes = null, bool loadText = false)
        {
            if (string.IsNullOrWhiteSpace(imgFile) || !File.Exists(imgFile))
            {
                throw new FileNotFoundException("Image file not found", imgFile);
            }

            using var img = new Mat(imgFile, ImreadModes.Color);
            var tableImage = new TableImage.TableImage(img);

            Rect? tableRect = null;
            if (tableBbox != null)
            {
                tableRect = new Rect(
                    (int)tableBbox.Value.X,
                    (int)tableBbox.Value.Y,
                    (int)tableBbox.Value.Width,
                    (int)tableBbox.Value.Height);
            }

            if (textBoxes == null)
            {
                textBoxes = OCRUtils.P_MaskTexts(img, Image2Table_WorkFolder);
            }
            List<Table> tables = tableImage.ExtractTables(_parameter.ImplicitRows, _parameter.ImplicitColumns, _parameter.DetectBorderlessTables, tableRect, textBoxes, isImage: true);

            if (loadText)
            {
                LoadText(textBoxes, tables, true);
            }
            
            var pagedTable = new PagedTable
            {
                PageCount = 1,
                PageIndex = 0,
                PageImage = imgFile,
                Tables = tables,
            };

            return pagedTable;
        }
        
        private void LoadText(IEnumerable<TextRect> textBoxes, List<Table> tables, bool fromOCR = false)
        {
            List<Cell> pageTextCells = textBoxes
                .Select(tb => new Cell(tb.Rect.X, tb.Rect.Y, tb.Rect.Right, tb.Rect.Bottom, tb.Text))
                .ToList();
            foreach (var table in tables)
            {
                foreach (var row in table.Rows)
                {
                    LoadRowText(row, pageTextCells, _parameter, useHtml: false, fromOCR: true);
                }
            }
        }

        // TODO: refact
        public static void LoadRowText(Row row, List<Cell> pageTextCells, TabularParameter parameter, bool useHtml = false, bool fromOCR = false)
        {
            foreach (var cell in row.Cells)
            {
                if (!string.IsNullOrEmpty(cell.Content))
                {
                    continue;
                }

                var cellRect = cell.Rect();
                var allTextCells = FindTextElement(cellRect, pageTextCells, parameter, fromOCR);

                if (allTextCells.Count > 0)
                {
                    Cell prev = null;
                    foreach (var curr in allTextCells)
                    {
                        bool isListParagraphBegin = TextElement.IsListParagraphBegin(curr.Content, out var ordered, out var listTag);
                        if (ordered)
                        {
                            isListParagraphBegin = false;
                        }
                        
                        if (prev != null)
                        {
                            if (prev.Baseline == curr.Baseline)
                            {
                                isListParagraphBegin = false;
                            }
                        }

                        if (useHtml && !string.IsNullOrEmpty(curr.HtmlContent))
                        {
                            string newLineText = curr.HtmlContent;
                            if (isListParagraphBegin)
                            {
                                newLineText = "<br />" + newLineText;
                            }
                            else
                            {
                                if (prev != null)
                                {
                                    if (Cell.IsSpaceBetween(prev, curr) || prev.Y2 < curr.Y1 ) // prev.Baseline != curr.Baseline
                                    {
                                        newLineText = " " + newLineText;
                                    }
                                }
                            }
                            cell.AddText(newLineText);
                        }
                        else
                        {
                            string newLineText = curr.Content;
                            if (isListParagraphBegin)
                            {
                                newLineText = "\n\n" + newLineText;
                            }
                            else
                            {
                                if (prev != null)
                                {
                                    if (Cell.IsSpaceBetween(prev, curr) || prev.Y2 < curr.Y1) // prev.Baseline != curr.Baseline
                                    {
                                        newLineText = " " + newLineText;
                                    }
                                }
                            }
                            cell.AddText(newLineText);
                        }

                        pageTextCells.Remove(curr);
                        prev = curr;
                    }
                }
            }
        }

        public static bool IsNewParagraphBegin(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            text = text.Trim();
            char c = text[0];
            if (char.IsUpper(c))
            {
                return true;
            }
                
            if (char.IsDigit(c))
            {
                return true;
            }

            return TextElement.IsListParagraphBegin(text, out _, out _);
        }

        private static List<Cell> FindTextElement(RectangleF cellRect, List<Cell> textCells, TabularParameter parameter, bool fromOCR = false)
        {
            var cells = new List<Cell>();
            foreach (var tc in textCells)
            {
                var textRect = tc.Rect();
                bool contained = IsContained(cellRect, textRect, parameter, fromOCR);
                if (contained)
                {
                    cells.Add(tc);
                }
            }

            var cellGroups = GroupCellsByLine(cells);
            var lines = new List<Cell>();
            foreach (var group in cellGroups)
            {
                lines.AddRange(group);
            }
            return lines;
        }

        private static List<List<Cell>> GroupCellsByLine(List<Cell> cells)
        {
            List<List<Cell>> lines = new List<List<Cell>>();
            if (cells.Count == 0)
            {
                return lines;
            }
            if (cells.Count == 1)
            {
                lines.Add(cells);
                return lines;
            }

            var copy = cells.ToList();
            int top = copy.Min(c => Math.Min(c.Y1, c.Y2));
            int bottom = copy.Max(c => Math.Max(c.Y1, c.Y2));

            List<Cell> line = new List<Cell>();
            for (int i = top + 1; i <= bottom; i++)
            {
                var temp = new List<Cell>();
                foreach (var cell in copy)
                {
                    if (i >= cell.Y1 && i <= cell.Y2)
                    {
                        temp.Add(cell);
                    }
                }

                if (temp.Count() > 0)
                {
                    copy.RemoveAll(c => temp.Contains(c));
                    int currBottom = temp.Max(c => Math.Max(c.Y1, c.Y2));
                    foreach (var c in copy)
                    {
                        if (c.Y1 <= currBottom)
                        {
                            temp.Add(c);
                        }
                    }
                    copy.RemoveAll(c => temp.Contains(c));
                    line.AddRange(temp);

                    if (copy.Count() > 0)
                    {
                        i = temp.Max(c => Math.Max(c.Y1, c.Y2)) + 1;
                    }
                    else
                    {
                        lines.Add(line.OrderBy(c => c.X1).ToList());
                        break;
                    }
                }
                else
                {
                    if (line.Count() > 0)
                    {
                        lines.Add(line.OrderBy(c => c.X1).ToList());
                        line = new List<Cell>();
                    }
                }
            }

            return lines;
        }

        private static bool IsContained(RectangleF container, RectangleF dst, TabularParameter parameter, bool fromOCR = false)
        {
            RectangleF intersection = RectangleF.Intersect(container, dst);

            if (intersection.IsEmpty)
            {
                return false;
            }

            if (intersection.Equals(dst))
            {
                return true;
            }

            float intersectionArea = intersection.Width * intersection.Height;
            float dstArea = dst.Width * dst.Height;

            var ratio = fromOCR ? parameter.OCRCellTextOverlapRatio : parameter.CellTextOverlapRatio;
            return intersectionArea / dstArea >= ratio;
        }
    }
}
