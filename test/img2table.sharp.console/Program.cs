using img2table.sharp.Img2table.Sharp.Data;
using Img2table.Sharp.Tabular;
using Img2table.Sharp.Tabular.TableImage;
using Img2table.Sharp.Tabular.TableImage.TableElement;
using OpenCvSharp;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Local;
using Sdcb.PaddleOCR.Models.Online;
using System;
using System.Net.NetworkInformation;
using static PDFDict.SDK.Sharp.Core.OCR.TesseractOCR;

namespace img2table.sharp.console
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TabularPDF();

            // TabularImage();

            // Paddle();
        }

        private static void Paddle()
        {
            var modelName = "ch_ppstructure_mobile_v2.0_SLANet";
            OnlineTableRecognitionModel tableOnlineModel = OnlineTableRecognitionModel.All.Single(x => x.Name == modelName);
            TableRecognitionModel tableModel = tableOnlineModel.DownloadAsync().Result;
            using PaddleOcrTableRecognizer tableRec = new(tableModel);

            var tempFile = Path.Combine(Environment.CurrentDirectory, @"Files/t1.png");
            using Mat src = Cv2.ImRead(tempFile);
            TableDetectionResult result = tableRec.Run(src);
            
            Console.WriteLine(result.StructureBoxes);
            Console.WriteLine(result.HtmlTags);
            Console.WriteLine(result.Score > 0.9f);
            using Mat visualized = result.Visualize(src, Scalar.LightGreen);
            Cv2.ImWrite(@"C:\temp\table-visualized.jpg", visualized);

            Task<PaddleOcrResult> ocrResultTask = Task.Run(() =>
            {
                using PaddleOcrAll all = new(LocalFullModels.ChineseV3);
                all.Detector.UnclipRatio = 1.2f;
                return all.Run(src);
            });
            PaddleOcrResult ocrResult = ocrResultTask.Result;
            string html = result.RebuildTable(ocrResult);

            for (int i = 0; i < ocrResult.Regions.Length; ++i)
            {
                PaddleOcrResultRegion region = ocrResult.Regions[i];
                Rect ocrBox = Extend(region.Rect.BoundingRect(), 0);
                Cv2.Rectangle(src, ocrBox, Scalar.Red, 1);
            }

            using (new Window("dst image", src))
            {
                Cv2.WaitKey();
            }

            Cv2.ImWrite(@"C:\temp\table-visualized_1.jpg", src);

            Console.WriteLine(html);
            // output html to file
            string htmlFile = @"C:\temp\table-visualized_1.html";
            using (StreamWriter writer = new StreamWriter(htmlFile))
            {
                writer.WriteLine(html);
            }
        }

        public static float Distance(in Rect box1, in Rect box2)
        {
            int x1_1 = box1.X;
            int y1_1 = box1.Y;
            int x2_1 = box1.Right;
            int y2_1 = box1.Bottom;

            int x1_2 = box2.X;
            int y1_2 = box2.Y;
            int x2_2 = box2.Right;
            int y2_2 = box2.Bottom;

            float dis = Math.Abs(x1_2 - x1_1) + Math.Abs(y1_2 - y1_1) + Math.Abs(x2_2 - x2_1) + Math.Abs(y2_2 - y2_1);
            float dis_2 = Math.Abs(x1_2 - x1_1) + Math.Abs(y1_2 - y1_1);
            float dis_3 = Math.Abs(x2_2 - x2_1) + Math.Abs(y2_2 - y2_1);
            return dis + Math.Min(dis_2, dis_3);
        }

        public static float IntersectionOverUnion(in Rect box1, in Rect box2)
        {
            int x1 = Math.Max(box1.X, box2.X);
            int y1 = Math.Max(box1.Y, box2.Y);
            int x2 = Math.Min(box1.Right, box2.Right);
            int y2 = Math.Min(box1.Bottom, box2.Bottom);

            if (y1 >= y2 || x1 >= x2)
            {
                return 0.0f;
            }

            int intersectArea = (x2 - x1) * (y2 - y1);
            int box1Area = box1.Width * box1.Height;
            int box2Area = box2.Width * box2.Height;
            int unionArea = box1Area + box2Area - intersectArea;

            return (float)intersectArea / unionArea;
        }

        public static Rect Extend(in Rect rect, int extendLength)
        {
            return Rect.FromLTRB(rect.Left - extendLength, rect.Top - extendLength, rect.Right + extendLength, rect.Bottom + extendLength);
        }

        private static void TabularImage()
        {
            //string tempFile = Path.Combine(Environment.CurrentDirectory, @"Files/jd invoice.png");
            //string tempFile = @"C:\Users\MikeLi\Downloads\512a72c1-6936-47ac-93ea-58d29c84c7de.png";
            string tempFile = Path.Combine(Environment.CurrentDirectory, @"Files/t4.png");
            Console.WriteLine(tempFile);

            var param = TabularParameter.AutoDetect;
            param.CellTextOverlapRatio = 0.7f;
            var tableImage = new ImageTabular(param);
            var ret = tableImage.Process(tempFile, true);

            foreach (var t in ret.Tables)
            {
                Console.WriteLine(t.ToString());
            }

            using var img = new Mat(tempFile, ImreadModes.Color);
            DrawTables(img, ret.Tables);

            string html = Path.Combine(Environment.CurrentDirectory, @$"Files/b_{ret.PageIndex + 1}.html");
            TableHTML.Generate(new PagedTableDTO(ret), html);
            Console.WriteLine(html);

            string outputPath = @"C:/temp/img2table_data/borderless/temp.png";
            Cv2.ImWrite(outputPath, img);
            using (new Window("dst image", img))
            {
                Cv2.WaitKey();
            }
        }

        private static void TestImage()
        {
            string tempFile = Path.Combine(Environment.CurrentDirectory, @"Files/page2.png");
            // string tempFile = @"C:\Users\MikeLi\Downloads\6574bcde-6f04-43f2-9150-5b4b6e775439.png";
            
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
            string tempFile = Path.Combine(Environment.CurrentDirectory, @"Files/Normal Table.pdf");
            //string tempFile = @"C:\Users\MikeLi\Downloads\11068a08-c991-4511-b644-aa7840833616.PDF";
            //string tempFile = @"C:\dev\testfiles\pdfs\tagged\CoB Playbook - FY24.PDF";

            Console.WriteLine(tempFile);

            var param = TabularParameter.AutoDetect;
            param.DetectBorderlessTables = true;
            param.CellTextOverlapRatio = 0.7f;
            param.RenderResolution = 72;
            var pdfTabular = new PDFTabular(param);
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
            Scalar rectangleColor = new Scalar(0, 255, 0); // Red color (BGR format)

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
