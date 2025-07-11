using Img2table.Sharp.Tabular.TableImage.TableElement;
using OpenCvSharp;
using PDFDict.SDK.Sharp.Core.OCR;
using Sdcb.PaddleOCR.Models.Local;
using Sdcb.PaddleOCR;
using System.Drawing;
using System.Text;
using PDFDict.SDK.Sharp.Core.Contents;
using img2table.sharp.Img2table.Sharp.Tabular;

namespace Img2table.Sharp.Tabular
{
    public class ImageTabular
    {
        private TabularParameter _parameter;

        public ImageTabular(TabularParameter tabularParameter) 
        {
            _parameter = tabularParameter;
        }

        public PagedTable Process(string imgFile, RectangleF? tableBbox = null, bool loadText = false)
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
            List<Table> tables = tableImage.ExtractTables(_parameter.ImplicitRows, _parameter.ImplicitColumns, _parameter.DetectBorderlessTables, tableRect);

            if (loadText)
            {
                PaddleOCR(imgFile, tables);
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

        private void PaddleOCR(string imageFile, List<Table> tables)
        {
            var pageTextCells = OCRUtils.PaddleOCR(imageFile);
            foreach (var table in tables)
            {
                foreach (var row in table.Rows)
                {
                    LoadRowText(row, pageTextCells, _parameter);
                }
            }
        }

        private void LoadText(string imageFile, List<Table> tables)
        {
            var wordList = TesseractOCR.OCRWordLevel(imageFile);

            var buf = new StringBuilder();
            var pageTextCells = wordList.Select(word =>
            {
                var left = (int)Math.Round(word.BBox.Left);
                var top = (int)Math.Round(word.BBox.Top);
                var right = (int)Math.Round(word.BBox.Right);
                var bottom = (int)Math.Round(word.BBox.Bottom);

                buf.Append($"{word.Text}");
                return new Cell(left, top, right, bottom, word.Text);
            }).ToList();

            foreach (var table in tables)
            {
                foreach (var row in table.Rows)
                {
                    LoadRowText(row, pageTextCells, _parameter);
                }
            }
        }

        public static void LoadRowText(Row row, List<Cell> pageTextCells, TabularParameter parameter, bool useHtml = false)
        {
            foreach (var cell in row.Cells)
            {
                var cellRect = cell.Rect();
                var oneTextCells = FindTextElement(cellRect, pageTextCells, parameter);

                if (oneTextCells.Count > 0)
                {
                    Cell prev = null;
                    foreach (var curr in oneTextCells)
                    {
                        bool isListParagraphBegin = TextElement.IsListParagraphBegin(curr.Content);
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
                                    if (Cell.IsSpaceBetween(prev, curr) || prev.Baseline != curr.Baseline)
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
                                    if (Cell.IsSpaceBetween(prev, curr) || prev.Baseline != curr.Baseline)
                                    {
                                        newLineText = " " + newLineText;
                                    }
                                }
                            }
                            cell.AddText(newLineText);
                        }

                        pageTextCells.Remove(curr);
                        prev = curr;


                        //string text = useHtml ? tc.HtmlContent : tc.Content;
                        //if (prev != null)
                        //{
                        //    if (prev.Baseline == tc.Baseline)
                        //    {
                        //        cell.AddText(text, true);
                        //    }
                        //    else
                        //    {
                        //        if (IsNewParagraphBegin(tc.Content))
                        //        {
                        //            string newLineText = (useHtml ? "<br />" : "\n\n") + text;
                        //            cell.AddText(newLineText, true);
                        //        }
                        //        else
                        //        {
                        //            cell.AddText(text, true);
                        //        }
                        //    }
                        //}
                        //else
                        //{
                        //    cell.AddText(text, true);
                        //}
                        
                        //pageTextCells.Remove(tc);
                        //prev = tc;
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

            return TextElement.IsListParagraphBegin(text);
        }

        private static List<Cell> FindTextElement(RectangleF cellRect, List<Cell> textCells, TabularParameter parameter)
        {
            var cells = new List<Cell>();
            foreach (var tc in textCells)
            {
                var textRect = tc.Rect();

                bool contained = IsContained(cellRect, textRect, parameter);
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

        public static List<List<Cell>> GroupCellsByLine(List<Cell> cells, float baselineTolerance = 3f)
        {
            var sorted = cells.OrderBy(c => c.Baseline).ToList();
            var groups = new List<List<Cell>>();

            foreach (var cell in sorted)
            {
                bool added = false;
                foreach (var group in groups)
                {
                    if (Math.Abs(group[0].Baseline - cell.Baseline) <= baselineTolerance)
                    {
                        group.Add(cell);
                        added = true;
                        break;
                    }
                }

                if (!added)
                {
                    groups.Add(new List<Cell> { cell });
                }
            }

            foreach (var group in groups)
            {
                group.Sort((a, b) => a.X1.CompareTo(b.X1));
            }

            return groups;
        }

        private static bool IsContained(RectangleF container, RectangleF dst, TabularParameter parameter)
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

            return intersectionArea / dstArea >= parameter.CellTextOverlapRatio;
        }
    }
}
