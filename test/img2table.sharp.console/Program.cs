using img2table.sharp.Img2table.Sharp.Data;
using Img2table.Sharp.Tabular;
using Img2table.Sharp.Tabular.TableImage;
using Img2table.Sharp.Tabular.TableImage.TableElement;
using OpenCvSharp;
using System;

namespace img2table.sharp.console
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TabularPDF();

            // TabularImage();
        }

        private static void TabularImage()
        {
            //string tempFile = Path.Combine(Environment.CurrentDirectory, @"Files/b.png");
            string tempFile = @"C:\Users\MikeLi\Downloads\512a72c1-6936-47ac-93ea-58d29c84c7de.png";

            Console.WriteLine(tempFile);

            var tableImage = new ImageTabular(TabularParameter.AutoDetect);
            var ret = tableImage.Process(tempFile, true);

            foreach (var t in ret.Tables)
            {
                Console.WriteLine(t.ToString());
            }

            using var img = new Mat(tempFile, ImreadModes.Color);
            DrawTables(img, ret.Tables);

            string outputPath = @"C:/temp/img2table_data/borderless/temp.png";
            Cv2.ImWrite(outputPath, img);
            using (new Window("dst image", img))
            {
                Cv2.WaitKey();
            }
        }

        private static void TestImage()
        {
            //string tempFile = Path.Combine(Environment.CurrentDirectory, @"Files/page2.png");
            string tempFile = @"C:\Users\MikeLi\Downloads\6574bcde-6f04-43f2-9150-5b4b6e775439.png";
            
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
            {
                Cv2.WaitKey();
            }
        }

        private static void TabularPDF()
        {
            // string tempFile = Path.Combine(Environment.CurrentDirectory, @"Files/b.pdf");
            // string tempFile = @"C:\Users\MikeLi\Downloads\11068a08-c991-4511-b644-aa7840833616.PDF";
            string tempFile = @"C:\dev\testfiles\pdfs\tables\Ninety-One-HK-GSF-Global-Multi-Asset-Income-Fund-Presentation-zh.pdf_v35.0.PDF";

            Console.WriteLine(tempFile);

            var pdfTabular = new PDFTabular(TabularParameter.AutoDetect);
            var tables = pdfTabular.Process(tempFile);

            int count = 0;
            foreach (var pt in tables)
            {
                if (pt.Tables?.Count == 0)
                {
                    continue;
                }

                if (pt.PageImage != null)
                {
                    using var img = new Mat(pt.PageImage, ImreadModes.Color);
                    DrawTables(img, pt.Tables);

                    string png = Path.Combine(Environment.CurrentDirectory, $@"Files/b_{pt.PageIndex + 1}.png");
                    Cv2.ImWrite(png, img);

                    using (new Window("dst image", img))
                    {
                        Cv2.WaitKey();
                    }
                }

                string html = Path.Combine(Environment.CurrentDirectory, @$"Files/b_{pt.PageIndex + 1}.html");
                TableHTML.Generate(new PagedTableDTO(pt), html);
                Console.WriteLine(html);

                string md = Path.Combine(Environment.CurrentDirectory, @$"Files/b_{pt.PageIndex + 1}.md");
                TableMarkdown.Generate(new PagedTableDTO(pt), md);
                Console.WriteLine(md);

                count++;
            }
        }

        private static void DrawTables(Mat img, List<Table> tables)
        {
            int thickness = 1;
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
        }

        private static void DrawTemp(Mat img, List<Cell> rects)
        {
            int thickness = 1;
            Scalar rectangleColor = new Scalar(255, 0, 0); // Red color (BGR format)

            foreach (var cell in rects)
            {
                Cv2.Rectangle(img, new Rect((int)cell.X1, (int)cell.Y1, (int)cell.Width, (int)cell.Height), rectangleColor, thickness);
            }
        }
    }
}
