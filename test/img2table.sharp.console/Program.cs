using img2table.sharp.Img2table.Sharp.Data;
using Img2table.Sharp.Tabular;
using Img2table.Sharp.Tabular.TableImage;
using Img2table.Sharp.Tabular.TableImage.TableElement;
using OpenCvSharp;
using PDFDict.SDK.Sharp.Tools;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Local;
using Sdcb.PaddleOCR.Models.Online;
using System.Drawing;

namespace img2table.sharp.console
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //PDFTools.Render(@"C:\Users\MikeLi\AppData\Local\Temp\image2table_9966acf1-c43b-465c-bf7f-dd3c30394676\c46fc46e-ac94-4458-8e6b-7213e3ce5577\toUnicodeMap.PDF",
            //    @"C:\Users\MikeLi\AppData\Local\Temp\image2table_9966acf1-c43b-465c-bf7f-dd3c30394676\c46fc46e-ac94-4458-8e6b-7213e3ce5577");


            // TabularPDF();

            //pre();
            //TabularImage();

            //RenderPDFPathOnly();
            // DetectLinesByHoughLinesP();

            //Paddle();

            //var tableBbox = RectangleF.FromLTRB(248, 393, 2293, 721);
            //TabularImage(tableBbox);
            //TableCellDetector.DetectTableCells(@"C:\dev\testfiles\ai_testsuite\pdf\table\z (1).png", tableBbox);

            SplitPDF();

            // ExtractAnnots();

            //TestPDFSDK();
        }

        static void ExtractAnnots()
        {
            var tempFile = @"C:\dev\testfiles\ai_testsuite\pdf\link\link_docx.pdf";
            Console.WriteLine(tempFile);

            var annots = PDFTools.ExtractAnnots(tempFile);
            foreach (var page in annots)
            {
                Console.WriteLine($"Page {page.Key}:");
                foreach (var annot in page.Value)
                {
                    Console.WriteLine($"  {annot}");
                }
            }
        }


        // 方法2：优化的 HoughLinesP 概率霍夫变换（通用直线检测）
        static void DetectLinesByHoughLinesP()
        {
            var tempFile = @"C:\dev\testfiles\ai_testsuite\pdf\table\x\ddd\page-3.png";
            Console.WriteLine(tempFile);

            Mat src = Cv2.ImRead(tempFile, ImreadModes.Color);

            // 灰度化
            Mat gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            // 预处理：高斯模糊去噪
            Mat blurred = new Mat();
            Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(3, 3), 0);

            // 自适应二值化（比简单阈值更好）
            Mat binary = new Mat();
            Cv2.AdaptiveThreshold(blurred, binary, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 15, -2);

            // 多种边缘检测参数组合
            Mat edges1 = new Mat();

            // 低阈值 - 检测更多边缘
            //Cv2.Canny(blurred, edges1, 30, 80);
            // 中等阈值 - 平衡检测
            Cv2.Canny(blurred, edges1, 50, 150);

            List<LineSegmentPoint> allLines = new List<LineSegmentPoint>();

            // 简化参数，避免过多重复
            var paramSets = new[]
            {
                new { threshold = 80, minLen = 30, maxGap = 5 },   // 主要检测
                new { threshold = 50, minLen = 20, maxGap = 3 }    // 补充短线
            };

            foreach (var param in paramSets)
            {
                LineSegmentPoint[] lines = Cv2.HoughLinesP(
                    edges1,
                    1,                    // 距离分辨率
                    Math.PI / 180,        // 角度分辨率
                    param.threshold,      // 阈值
                    minLineLength: param.minLen,
                    maxLineGap: param.maxGap
                );

                allLines.AddRange(lines);
            }

            // 只保留横线和竖线，过滤掉斜线
            var horizontalVerticalLines = allLines.Where(line =>
            {
                var type = GetLineType(line);
                return type == LineType.Horizontal || type == LineType.Vertical;
            }).ToList();

            // 增强去重算法
            var uniqueLines = RemoveDuplicateLinesAdvanced(horizontalVerticalLines);

            Console.WriteLine(horizontalVerticalLines.Count + " -- " + uniqueLines.Count);

            // 绘制直线
            foreach (var line in uniqueLines)
            {
                Cv2.Line(src, line.P1, line.P2, new Scalar(0, 255, 0), 1);
            }

            // 保存中间结果用于调试
            Cv2.ImWrite(@"C:\dev\testfiles\ai_testsuite\pdf\table\x\ddd\edges_combined.png", edges1);
            Cv2.ImWrite(@"C:\dev\testfiles\ai_testsuite\pdf\table\x\ddd\hough_optimized_result.png", src);
        }

        // 增强的去重算法
        static List<LineSegmentPoint> RemoveDuplicateLinesAdvanced(List<LineSegmentPoint> lines)
        {
            if (lines.Count == 0) return lines;

            var uniqueLines = new List<LineSegmentPoint>();

            // 按长度排序，优先保留较长的线
            var sortedLines = lines.OrderByDescending(line =>
                Math.Sqrt(Math.Pow(line.P2.X - line.P1.X, 2) + Math.Pow(line.P2.Y - line.P1.Y, 2))).ToList();

            foreach (var line in sortedLines)
            {
                bool shouldAdd = true;

                for (int i = 0; i < uniqueLines.Count; i++)
                {
                    var existingLine = uniqueLines[i];

                    // 多种相似性检测
                    if (AreLinesOverlapping(line, existingLine) ||
                        AreLinesSimilarAdvanced(line, existingLine))
                    {
                        // 如果新线更长，替换现有线
                        double newLength = GetLineLength(line);
                        double existingLength = GetLineLength(existingLine);

                        if (newLength > existingLength * 1.2) // 新线明显更长
                        {
                            uniqueLines[i] = line;
                        }
                        shouldAdd = false;
                        break;
                    }
                }

                if (shouldAdd)
                {
                    uniqueLines.Add(line);
                }
            }

            return uniqueLines;
        }

        // 计算线段长度
        static double GetLineLength(LineSegmentPoint line)
        {
            return Math.Sqrt(Math.Pow(line.P2.X - line.P1.X, 2) + Math.Pow(line.P2.Y - line.P1.Y, 2));
        }

        // 检测两条线是否重叠（专门针对横线和竖线）
        static bool AreLinesOverlapping(LineSegmentPoint line1, LineSegmentPoint line2)
        {
            var type1 = GetLineType(line1);
            var type2 = GetLineType(line2);

            // 必须是同一类型
            if (type1 != type2) return false;

            if (type1 == LineType.Horizontal)
            {
                // 横线：检查Y坐标是否相近，X方向是否有重叠或连接
                double avgY1 = (line1.P1.Y + line1.P2.Y) / 2.0;
                double avgY2 = (line2.P1.Y + line2.P2.Y) / 2.0;

                if (Math.Abs(avgY1 - avgY2) <= 2) // Y坐标相近
                {
                    return HasHorizontalOverlap(line1, line2);
                }
            }
            else if (type1 == LineType.Vertical)
            {
                // 竖线：检查X坐标是否相近，Y方向是否有重叠或连接
                double avgX1 = (line1.P1.X + line1.P2.X) / 2.0;
                double avgX2 = (line2.P1.X + line2.P2.X) / 2.0;

                if (Math.Abs(avgX1 - avgX2) <= 2) // X坐标相近
                {
                    return HasVerticalOverlap(line1, line2);
                }
            }

            return false;
        }

        // 改进的相似性检测（只考虑横线和竖线）
        static bool AreLinesSimilarAdvanced(LineSegmentPoint line1, LineSegmentPoint line2)
        {
            // 获取线条类型（横线或竖线）
            var type1 = GetLineType(line1);
            var type2 = GetLineType(line2);

            // 必须是同一类型（都是横线或都是竖线）
            if (type1 != type2) return false;

            if (type1 == LineType.Horizontal)
            {
                // 横线：Y坐标相近
                double avgY1 = (line1.P1.Y + line1.P2.Y) / 2.0;
                double avgY2 = (line2.P1.Y + line2.P2.Y) / 2.0;
                if (Math.Abs(avgY1 - avgY2) > 3) return false; // Y坐标差小于3像素

                // 检查X方向的重叠
                return HasHorizontalOverlap(line1, line2);
            }
            else if (type1 == LineType.Vertical)
            {
                // 竖线：X坐标相近
                double avgX1 = (line1.P1.X + line1.P2.X) / 2.0;
                double avgX2 = (line2.P1.X + line2.P2.X) / 2.0;
                if (Math.Abs(avgX1 - avgX2) > 3) return false; // X坐标差小于3像素

                // 检查Y方向的重叠
                return HasVerticalOverlap(line1, line2);
            }

            return false;
        }

        // 线条类型枚举
        enum LineType
        {
            Horizontal,
            Vertical,
            Other
        }

        // 获取线条类型
        static LineType GetLineType(LineSegmentPoint line)
        {
            double deltaX = Math.Abs(line.P2.X - line.P1.X);
            double deltaY = Math.Abs(line.P2.Y - line.P1.Y);

            // 判断是横线还是竖线（允许5度以内的倾斜）
            if (deltaY <= deltaX * 0.087) // tan(5°) ≈ 0.087
                return LineType.Horizontal;
            else if (deltaX <= deltaY * 0.087)
                return LineType.Vertical;
            else
                return LineType.Other;
        }

        // 检查横线的X方向重叠
        static bool HasHorizontalOverlap(LineSegmentPoint line1, LineSegmentPoint line2)
        {
            double x1_start = Math.Min(line1.P1.X, line1.P2.X);
            double x1_end = Math.Max(line1.P1.X, line1.P2.X);
            double x2_start = Math.Min(line2.P1.X, line2.P2.X);
            double x2_end = Math.Max(line2.P1.X, line2.P2.X);

            // 检查是否有重叠或接近
            return x1_start <= x2_end + 5 && x2_start <= x1_end + 5; // 允许5像素间隙
        }

        // 检查竖线的Y方向重叠
        static bool HasVerticalOverlap(LineSegmentPoint line1, LineSegmentPoint line2)
        {
            double y1_start = Math.Min(line1.P1.Y, line1.P2.Y);
            double y1_end = Math.Max(line1.P1.Y, line1.P2.Y);
            double y2_start = Math.Min(line2.P1.Y, line2.P2.Y);
            double y2_end = Math.Max(line2.P1.Y, line2.P2.Y);

            // 检查是否有重叠或接近
            return y1_start <= y2_end + 5 && y2_start <= y1_end + 5; // 允许5像素间隙
        }



        static void TestPDFSDK()
        {
            var tempFile = @"C:\dev\testfiles\ai_testsuite\pdf\text_box_test\25-32G-6_BG_XMB.pdf";
            var tempFolder = @"C:\dev\testfiles\ai_testsuite\pdf\text_box_test";
            PDFTools.DrawTextBox(tempFile, tempFolder);
        }

        static void SplitPDF()
        {
            var src = @"C:\dev\testfiles\ai_testsuite\pdf\Cracking Test Data\test_report\image_tables\table_style.pdf";
            var dst = @"C:\dev\testfiles\ai_testsuite\pdf\Cracking Test Data\test_report\image_tables";
            var range = new List<int[]>();
            //range.Add(new int[] { 0 });
            range.Add(new int[] { 1, 2 });
            range.Add(new int[] { 3, 4 });
            PDFTools.SplitPDF(src, range, dst);
        }

        static void pre()
        {
            var tempFile = @"C:\dev\testfiles\ai_testsuite\pdf\table\z.png";
            using Mat src = Cv2.ImRead(tempFile);

            //Task<PaddleOcrResult> ocrResultTask = Task.Run(() =>
            //{
            //    using PaddleOcrAll all = new(LocalFullModels.ChineseV3);
            //    all.Detector.UnclipRatio = 1.2f;
            //    return all.Run(src);
            //});
            //PaddleOcrResult ocrResult = ocrResultTask.Result;

            //for (int i = 0; i < ocrResult.Regions.Length; ++i)
            //{
            //    PaddleOcrResultRegion region = ocrResult.Regions[i];
            //    Rect ocrBox = Extend(region.Rect.BoundingRect(), -2);
            //    Cv2.Rectangle(src, ocrBox, Scalar.White, -1);
            //    FillRectDashedVertical(src, ocrBox, Scalar.Black, 16, 8);
            //}

            Cv2.ImWrite(@"C:\temp\table-visualized_1.jpg", src);
        }

        static void FillRectDashedVertical(Mat img, Rect rect, Scalar color, int dashWidth = 10, int gap = 2)
        {
            for (int x = rect.Left; x < rect.Right; x += dashWidth + gap)
            {
                int xEnd = Math.Min(x + dashWidth, rect.Right);
                var dashedRect = new Rect(x, rect.Top, xEnd - x, rect.Height);
                Cv2.Rectangle(img, dashedRect, color, thickness: -1);
            }
        }

        static void TT()
        {
            //string tempFile = @"C:\dev\testfiles\ai_testsuite\pdf\table\line1.png";
            string tempFile = @"C:\dev\testfiles\ai_testsuite\pdf\table\line1.png";

            tempFile = @"C:\dev\testfiles\ai_testsuite\pdf\table\x\ddd\page-1.png";
            // 1. 读取图片
            Mat src = Cv2.ImRead(tempFile, ImreadModes.Color);

            // 2. 转为灰度图
            Mat gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            // 3. 边缘检测
            Mat edges = new Mat();
            Cv2.Canny(gray, edges, 50, 150);

            // 4. 霍夫直线检测
            LineSegmentPoint[] lines = Cv2.HoughLinesP(
                edges,           // 输入的二值图像
                1,               // 距离分辨率
                Math.PI / 180,   // 角度分辨率
                400,             // 阈值
                minLineLength: 20,
                maxLineGap: 2
            );

            // 5. 绘制检测到的直线
            foreach (var line in lines)
            {
                Cv2.Line(src, line.P1, line.P2, new Scalar(0, 255, 0), 4);
            }

            Cv2.ImWrite(@"C:\dev\testfiles\ai_testsuite\pdf\table\x\ddd\xxx.png", src);

            // 6. 保存或显示结果
            // 设定最大显示宽高
            int maxWidth = 1200;
            int maxHeight = 800;

            // 获取原图尺寸
            int width = src.Width;
            int height = src.Height;

            // 计算缩放比例
            double scale = Math.Min((double)maxWidth / width, (double)maxHeight / height);
            if (scale > 1) scale = 1; // 不放大

            int newWidth = (int)(width * scale);
            int newHeight = (int)(height * scale);

            // 缩放图片
            Mat resized = new Mat();
            Cv2.Resize(src, resized, new OpenCvSharp.Size(newWidth, newHeight));

            // 显示
            Cv2.ImShow("Detected Lines", resized);
            Cv2.WaitKey();
        }

        private static void RenderPDFPathOnly()
        {
            string pdfPath = @"C:\dev\testfiles\ai_testsuite\pdf\table\x\itot-ishares-core-s-p-total-u-s-stock-market-etf-fund-fact-sheet-en-us.pdf";
            string outputPath = @"C:\dev\testfiles\ai_testsuite\pdf\table\x\ddd";

            Console.WriteLine($"Rendering PDF: {pdfPath}");
            Console.WriteLine($"Output Path: {outputPath}");

            // Render the PDF to an image
            PDFTools.RenderTableEnhanced(pdfPath, outputPath, 300f);
        }

        private static void Paddle()
        {
            var modelName = "ch_ppstructure_mobile_v2.0_SLANet";
            OnlineTableRecognitionModel tableOnlineModel = OnlineTableRecognitionModel.All.Single(x => x.Name == modelName);
            TableRecognitionModel tableModel = tableOnlineModel.DownloadAsync().Result;
            using PaddleOcrTableRecognizer tableRec = new(tableModel);

            var tempFile = Path.Combine(Environment.CurrentDirectory, @"Files/t1.png");
            tempFile = @"C:\dev\testfiles\ai_testsuite\pdf\table\g.png";


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

            Cv2.ImWrite(@"C:\dev\testfiles\ai_testsuite\pdf\table\temp.png", src);

            Console.WriteLine(html);
            // output html to file
            string htmlFile = @"C:\dev\testfiles\ai_testsuite\pdf\table\temp.html";
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

        private static void TabularImage(RectangleF tableRect)
        {
            //string tempFile = Path.Combine(Environment.CurrentDirectory, @"Files/jd invoice.png");
            //string tempFile = @"C:\Users\MikeLi\Downloads\512a72c1-6936-47ac-93ea-58d29c84c7de.png";
            string tempFile = Path.Combine(Environment.CurrentDirectory, @"Files/a.png");

            tempFile = @"C:\dev\testfiles\ai_testsuite\pdf\table\z (1).png";
            Console.WriteLine(tempFile);

            var param = TabularParameter.AutoDetect;
            param.DetectBorderlessTables = false;
            param.CellTextOverlapRatio = 0.6f;
            var tableImage = new ImageTabular(param);
            var ret = tableImage.Process(tempFile, tableRect, null, true);

            foreach (var t in ret.Tables)
            {
                Console.WriteLine(t.ToString());
            }

            using var img = new Mat(tempFile, ImreadModes.Color);
            DrawTables(img, ret.Tables);

            string html = Path.Combine(Environment.CurrentDirectory, @$"Files/b_{ret.PageIndex + 1}.html");
            TableHTML.Generate(new PagedTableDTO(ret), html);
            Console.WriteLine(html);

            string outputPath = @"C:\dev\testfiles\ai_testsuite\pdf\table\temp.png";
            Cv2.ImWrite(outputPath, img);
            //using (new Window("dst image", img))
            //{
            //    Cv2.WaitKey();
            //}
        }

        private static void TabularImage()
        {
            //string tempFile = Path.Combine(Environment.CurrentDirectory, @"Files/jd invoice.png");
            //string tempFile = @"C:\Users\MikeLi\Downloads\512a72c1-6936-47ac-93ea-58d29c84c7de.png";
            string tempFile = Path.Combine(Environment.CurrentDirectory, @"Files/a.png");

            tempFile = @"C:\dev\testfiles\ai_testsuite\pdf\table\z (5).png";
            Console.WriteLine(tempFile);

            var param = TabularParameter.AutoDetect;
            param.DetectBorderlessTables = false;
            param.CellTextOverlapRatio = 0.6f;
            var tableImage = new ImageTabular(param);
            var ret = tableImage.Process(tempFile, loadText: false);

            foreach (var t in ret.Tables)
            {
                Console.WriteLine(t.ToString());
            }

            using var img = new Mat(tempFile, ImreadModes.Color);
            DrawTables(img, ret.Tables);

            string html = Path.Combine(Environment.CurrentDirectory, @$"Files/b_{ret.PageIndex + 1}.html");
            TableHTML.Generate(new PagedTableDTO(ret), html);
            Console.WriteLine(html);

            string outputPath = @"C:\dev\testfiles\ai_testsuite\pdf\table\temp.png";
            Cv2.ImWrite(outputPath, img);
            //using (new Window("dst image", img))
            //{
            //    Cv2.WaitKey();
            //}
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
            param.RenderResolution = 144f;
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
