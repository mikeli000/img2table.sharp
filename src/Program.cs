using Img2table.Sharp.Img2table.Sharp.Data;
using Img2table.Sharp.Tabular;
using Img2table.Sharp.Tabular.TableElement;
using Img2table.Sharp.Tabular.TextLoader;
using OpenCvSharp;
using System.Drawing;

namespace Img2table.Sharp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // TabularPDF();

            TabularImage();
        }

        private static void TabularImage()
        {
            var tempFile = @"C:/temp/img2table_data/borderless/4.png";
            Console.WriteLine(tempFile);

            var tableImage = new ImageTabular();
            var ret = tableImage.Process(tempFile, true);

            foreach (var t in ret.Tables)
            {
                Console.WriteLine(t.ToString());
            }

            using var img = new Mat(tempFile, ImreadModes.Color);
            DrawTables(img, ret.Tables);

            using (new Window("dst image", img))
            Cv2.WaitKey();
        }

        private static void TestImage()
        {
            var tempFile = @"C:/temp/img2table_data/borderless/page2.png";
            Console.WriteLine(tempFile);

            using var img = new Mat(tempFile, ImreadModes.Color);
            var tableImage = new TableImage(img);
            List<Table> tables = tableImage.ExtractTables(false, false, true);
            foreach (var t in tables)
            {
                Console.WriteLine(t.ToString());
            }

            DrawTables(img, tables);

            using (new Window("dst image", img))
            Cv2.WaitKey();
        }

        private static void TabularPDF()
        {
            var tempFile = @"C:/temp/img2table_data/borderless/table_style.pdf";
            Console.WriteLine(tempFile);

            var pdfTabular = new PDFTabular();
            var tables = pdfTabular.Process(tempFile);

            foreach (var pt in tables)
            {
                foreach (var t2 in pt.Tables)
                {
                    Console.WriteLine(t2.ToString());
                }

                using var img = new Mat(pt.PageImage, ImreadModes.Color);
                DrawTables(img, pt.Tables);

                using (new Window("dst image", img))
                Cv2.WaitKey();
            }
        }

        private static void DrawTables(Mat img, List<Table> tables)
        {
            int thickness = 2;
            Scalar rectangleColor = new Scalar(0, 0, 255); // Red color (BGR format)
            
            foreach (Table table in tables)
            {
                foreach (var row in table.Items)
                {
                    foreach (var cell in row.Items)
                    {
                        Cv2.Rectangle(img, new Rect(cell.X1, cell.Y1, cell.Width, cell.Height), rectangleColor, thickness);
                    }
                }
            }

            string outputPath = @"C:/temp/img2table_data/borderless/temp.png";
            Cv2.ImWrite(outputPath, img);
        }

        private static void DrawTemp(Mat img, List<RectangleF> rects)
        {
            int thickness = 1;
            Scalar rectangleColor = new Scalar(255, 0, 0); // Red color (BGR format)

            foreach (var cell in rects)
            {
                Cv2.Rectangle(img, new Rect((int)cell.X, (int)cell.Y, (int)cell.Width, (int)cell.Height), rectangleColor, thickness);
            }
        }
    }
}
