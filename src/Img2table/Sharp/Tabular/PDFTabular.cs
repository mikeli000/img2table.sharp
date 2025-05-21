using Img2table.Sharp.Tabular.TableImage.TableElement;
using PDFDict.SDK.Sharp.Core;
using PDFDict.SDK.Sharp.Core.Contents;
using System.Drawing;
using System.Text;

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
                    // step 1: check if page is tagged, if yes, use tagged content
                    var page = pdfDoc.LoadPage(i);
                    if (page.IsTagged())
                    {
                        var pagedTables = ExtractTableFromTaggedPDF(pdfDoc, page);
                        allTables.AddRange(pagedTables);
                        continue;
                    }

                    string pageImagePath = Path.Combine(outputFolder, @$"page{i + 1}.png");
                    pdfDoc.RenderPage(pageImagePath, i, _parameter.RenderResolution, backgroundColor: Color.White);

                    var imageTabular = new ImageTabular(_parameter);
                    var pagedTable = imageTabular.Process(pageImagePath, false);

                    allTables.Add(pagedTable);

                    //LoadText(pdfDoc, i, pagedTable, _parameter.RenderResolution / 72f);
                    LoadText(pdfDoc, i, pagedTable, 1f);
                }
            }

            return allTables;
        }

        private List<PagedTable> ExtractTableFromTaggedPDF(PDFDocument doc, PDFPage page)
        {
            var tables = new List<PagedTable>();
            PagedTable pagedTable = new PagedTable();
            pagedTable.PageIndex = page.GetPageIndex();
            pagedTable.PageCount = doc.GetPageCount();
            pagedTable.Tables = new List<Table>();
            tables.Add(pagedTable);

            var structTree = new PDFStructTree(page);
            int count = structTree.GetChildCount();
            for (int j = 0; j < count; j++)
            {
                var structElement = structTree.GetChild(j);
                TransStructElement(structElement, pagedTable);
            }

            return tables;
        }

        private static void TransStructElement(PDFStructElement structElement, PagedTable pagedTable)
        {
            if (structElement.ChildCount == -1)
            {
                return;
            }

            if (structElement.ChildCount == 0)
            {
                Console.WriteLine(structElement);
            }
            else
            {
                Console.WriteLine(structElement);
                for (int i = 0; i < structElement.ChildCount; i++)
                {
                    var child = structElement.GetChild(i);
                    if (string.Equals(child.Type, "Table", StringComparison.OrdinalIgnoreCase))
                    {
                        var table = new Table(new List<Row>());
                        pagedTable.Tables.Add(table);
                    }
                    else if (string.Equals(child.Type, "THead", StringComparison.OrdinalIgnoreCase))
                    {
                        var table = pagedTable.Tables.Last();
                        table.Rows.Add(new Row());
                    }
                    else if (string.Equals(child.Type, "TR", StringComparison.OrdinalIgnoreCase))
                    {
                        var table = pagedTable.Tables.Last();
                        table.Rows.Add(new Row());
                    }
                    else if (string.Equals(child.Type, "TH", StringComparison.OrdinalIgnoreCase))
                    {
                        var table = pagedTable.Tables.Last();
                        StringBuilder innerText = new StringBuilder();
                        ReadInnerText(child, innerText);

                        Cell cell = new Cell(0, 0, 0, 0, innerText.ToString());
                        table.Rows.Last().Cells.Add(cell);
                    }
                    else if (string.Equals(child.Type, "TD", StringComparison.OrdinalIgnoreCase))
                    {
                        var table = pagedTable.Tables.Last();
                        StringBuilder innerText = new StringBuilder();
                        ReadInnerText(child, innerText);

                        Cell cell = new Cell(0, 0, 0, 0, innerText.ToString());
                        table.Rows.Last().Cells.Add(cell);
                    }

                    TransStructElement(child, pagedTable);
                }
            }
        }

        private static void ReadInnerText(PDFStructElement structElement, StringBuilder buf)
        {
            if (structElement.ChildCount == -1)
            {
                return;
            }

            if (structElement.ChildCount == 0)
            {
                return;
            }
            else
            {
                Console.WriteLine(structElement);
                for (int i = 0; i < structElement.ChildCount; i++)
                {
                    var child = structElement.GetChild(i);
                    if (child.ActualText != null)
                    {
                        buf.Append(child.ActualText);
                    }

                    ReadInnerText(child, buf);

                    if (string.Equals(child.Type, "P", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(child.Type, "LI", StringComparison.OrdinalIgnoreCase))
                    {
                        if (buf.Length > 0)
                        {
                            buf.Append(Environment.NewLine);
                        }
                    }
                }
            }
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
