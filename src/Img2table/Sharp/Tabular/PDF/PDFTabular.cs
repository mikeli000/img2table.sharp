using Img2table.Sharp.Tabular.Image;
using Img2table.Sharp.Tabular.TableElement;
using PDFDict.SDK.Sharp.Core;
using System.Drawing;

namespace Img2table.Sharp.Tabular.PDF
{
    public class PDFTabular
    {
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
            var tables = new List<PagedTable>();
            using (PDFDocument pdfDoc = PDFDocument.Load(pdfFile))
            {
                int pageCount = pdfDoc.GetPageCount();

                for (int i = 0; i < pageCount; i++)
                {
                    string pageImagePath = Path.Combine(outputFolder, @$"page{i + 1}.png");
                    pdfDoc.RenderPage(pageImagePath, i, backgroundColor: Color.White);

                    var imageTabular = new ImageTabular();
                    var pagedTable = imageTabular.Process(pageImagePath);

                    tables.Add(pagedTable);
                }
            }

            return tables;
        }

        private void LoadText(PDFDocument pdfDoc, int pageIndex, List<Table> tables)
        {

        }
    }
}
