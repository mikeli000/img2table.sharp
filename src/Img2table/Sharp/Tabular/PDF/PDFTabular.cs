using Img2table.Sharp.Tabular.Image;
using Img2table.Sharp.Tabular.TableElement;
using PDFDict.SDK.Sharp.Core;
using PDFDict.SDK.Sharp.Core.Contents;
using System.Drawing;

namespace Img2table.Sharp.Tabular.PDF
{
    public class PDFTabular
    {
        private static float DEFAULT_OVERLAP_RATIO = 0.98f;

        public PDFTabular() 
        { 
        }

        public List<PagedTable> Process(string pdfFile)
        {
            if (string.IsNullOrWhiteSpace(pdfFile) || !File.Exists(pdfFile))
            {
                throw new FileNotFoundException("PDF file not found", pdfFile);
            }

            string outputFolder = Path.GetTempPath();
            var allTables = new List<PagedTable>();
            using (PDFDocument pdfDoc = PDFDocument.Load(pdfFile))
            {
                int pageCount = pdfDoc.GetPageCount();

                for (int i = 0; i < pageCount; i++)
                {
                    string pageImagePath = Path.Combine(outputFolder, @$"page{i + 1}.png");
                    pdfDoc.RenderPage(pageImagePath, i, resolution:72f, backgroundColor: Color.White);

                    var imageTabular = new ImageTabular();
                    var pagedTable = imageTabular.Process(pageImagePath);
                    
                    allTables.Add(pagedTable);

                    LoadText(pdfDoc, i, pagedTable);
                }
            }

            return allTables;
        }

        private void LoadText(PDFDocument pdfDoc, int pageIndex, PagedTable pagedTable)
        {
            var pdfPage = pdfDoc.LoadPage(pageIndex);

            var pageThread = pdfPage.BuildPageThread();
            var textThread = pageThread.GetTextThread();
            var textElements = new List<TextElement>(textThread.GetTextElements());

            foreach (var table in pagedTable.Tables)
            {
                foreach (var row in table.Rows)
                {
                    LoadRowText(row, textElements);
                }
            }
        }

        private void LoadRowText(Row row, List<TextElement> textElements)
        {
            foreach (var cell in row.Cells)
            {
                var cellRect = cell.Rect();
                var ele = FindTextElement(cellRect, textElements);

                if (ele != null)
                {
                    cell.AddText(ele.GetText());
                    textElements.Remove(ele);
                }
            }
        }

        private TextElement FindTextElement(RectangleF cellRect, List<TextElement> textElements)
        {
            foreach (var textElement in textElements)
            {
                var textRect = textElement.BBox;

                bool contained = IsContained(cellRect, textRect);
                if (contained)
                {
                    return textElement;
                }
            }

            return null;
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

            return (intersectionArea / dstArea) >= DEFAULT_OVERLAP_RATIO;
        }
    }
}
