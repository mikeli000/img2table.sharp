using Img2table.Sharp.Tabular.TableImage.TableElement;
using PDFDict.SDK.Sharp.Core;
using PDFDict.SDK.Sharp.Core.Contents;
using System.Drawing;

namespace Img2table.Sharp.Tabular
{
    public class PDFTabular
    {
        private TabularParameter _parameter;

        public PDFTabular(TabularParameter tabularParameter)
        {
            _parameter = tabularParameter;
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
                    pdfDoc.RenderPage(pageImagePath, i, _parameter.RenderResolution, backgroundColor: Color.White);

                    var imageTabular = new ImageTabular(_parameter);
                    var pagedTable = imageTabular.Process(pageImagePath);

                    allTables.Add(pagedTable);

                    LoadText(pdfDoc, i, pagedTable, _parameter.RenderResolution / 72f);
                }
            }

            return allTables;
        }

        private void LoadText(PDFDocument pdfDoc, int pageIndex, PagedTable pagedTable, float ratio)
        {
            var pdfPage = pdfDoc.LoadPage(pageIndex);

            var pageThread = pdfPage.BuildPageThread();
            var textThread = pageThread.GetTextThread();
            var textElements = new List<TextElement>(textThread.GetTextElements());

            var pageTextCells = ScaleToCells(textElements, ratio, pdfPage.GetPageHeight());
            foreach (var table in pagedTable.Tables)
            {
                foreach (var row in table.Rows)
                {
                    ImageTabular.LoadRowText(row, pageTextCells, _parameter);
                }
            }
        }

        private List<Cell> ScaleToCells(List<TextElement> textElements, float ratio, double pageHeight)
        {
            List<Cell> cells = new List<Cell>();
            double ph = pageHeight * ratio;
            foreach (var ele in textElements)
            {
                int top = (int)Math.Round(ph - ele.BBox.Top * ratio - ele.BBox.Height * ratio);
                int bottom = (int)Math.Round(top + ele.BBox.Height * ratio);
                int left = (int)Math.Round(ele.BBox.Left * ratio);
                int right = (int)Math.Round(ele.BBox.Right * ratio);

                Cell c = new Cell(left, top, right, bottom, ele.GetText());
                cells.Add(c);
            }

            return cells;
        }
    }
}
