using Img2table.Sharp.Tabular.TableImage.TableElement;
using OpenCvSharp;
using PDFDict.SDK.Sharp.Core.OCR;
using System.Drawing;

namespace Img2table.Sharp.Tabular
{
    public class ImageTabular
    {
        private static float DEFAULT_OVERLAP_RATIO = 0.6f;
        

        public ImageTabular() 
        {

        }

        public PagedTable Process(string imgFile, bool loadText = false)
        {
            if (string.IsNullOrWhiteSpace(imgFile) || !File.Exists(imgFile))
            {
                throw new FileNotFoundException("Image file not found", imgFile);
            }

            using var img = new Mat(imgFile, ImreadModes.Color);
            var tableImage = new TableImage.TableImage(img);
            List<Table> tables = tableImage.ExtractTables(false, false, true);

            if (loadText)
            {
                LoadText(imgFile, tables);
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

        private void LoadText(string imageFile, List<Table> tables)
        {
            var wordList = TesseractOCR.OCRWordLevel(imageFile);

            var pageTextCells = wordList.Select(word =>
            {
                var left = (int)Math.Round(word.BBox.Left);
                var top = (int)Math.Round(word.BBox.Top);
                var right = (int)Math.Round(word.BBox.Right);
                var bottom = (int)Math.Round(word.BBox.Bottom);
                
                return new Cell(left, top, right, bottom, word.Text);
            }).ToList();

            foreach (var table in tables)
            {
                foreach (var row in table.Rows)
                {
                    LoadRowText(row, pageTextCells);
                }
            }
        }

        public static void LoadRowText(Row row, List<Cell> pageTextCells)
        {
            foreach (var cell in row.Cells)
            {
                var cellRect = cell.Rect();
                var oneTextCells = FindTextElement(cellRect, pageTextCells);

                if (oneTextCells.Count > 0)
                {
                    foreach (var tc in oneTextCells)
                    {
                        cell.AddText(tc.Content, true);
                        pageTextCells.Remove(tc);
                    }
                }
            }
        }

        private static List<Cell> FindTextElement(RectangleF cellRect, List<Cell> textCells)
        {
            var cells = new List<Cell>();
            foreach (var tc in textCells)
            {
                var textRect = tc.Rect();

                bool contained = IsContained(cellRect, textRect);
                if (contained)
                {
                    cells.Add(tc);
                }
            }

            return cells;
        }

        private static bool IsContained(RectangleF container, RectangleF dst)
        {
            RectangleF intersection = RectangleF.Intersect(container, dst);

            if (intersection.IsEmpty)
            {
                return false;
            }

            float intersectionArea = intersection.Width * intersection.Height;
            float dstArea = dst.Width * dst.Height;

            return intersectionArea / dstArea >= DEFAULT_OVERLAP_RATIO;
        }
    }
}
