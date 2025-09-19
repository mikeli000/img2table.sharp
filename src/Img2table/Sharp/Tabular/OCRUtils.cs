using Img2table.Sharp.Tabular.TableImage.TableElement;
using OpenCvSharp;
using Sdcb.PaddleOCR.Models.Local;
using PDFDict.SDK.Sharp.Core.OCR;
using Sdcb.PaddleOCR;
using System.Text;
using img2table.sharp.Img2table.Sharp.Tabular.TableImage;

namespace img2table.sharp.Img2table.Sharp.Tabular
{
    public class OCRUtils
    {
        public static List<Cell> PaddleOCR(string imageFile)
        {
            using Mat src = Cv2.ImRead(imageFile);
            using PaddleOcrAll all = new(LocalFullModels.ChineseV3);
            all.Detector.UnclipRatio = 1.2f;
            var ocrResult = all.Run(src);

            var pageTextCells = ocrResult.Regions.Select(word =>
            {
                var left = word.Rect.BoundingRect().Left;
                var top = word.Rect.BoundingRect().Top;
                var right = word.Rect.BoundingRect().Right;
                var bottom = word.Rect.BoundingRect().Bottom;

                var c = new Cell(left, top, right, bottom, word.Text);
                c.Baseline = bottom;
                return c;
            }).ToList();

            return pageTextCells;
        }

        public static List<TextRect> P_MaskTexts(Mat srcImage, string tempDir)
        {
            using PaddleOcrAll all = new(LocalFullModels.ChineseV3);
            all.Detector.UnclipRatio = 0.5f;
            var ocrResult = all.Run(srcImage);

            var textRects = new List<TextRect>();
            foreach (var word in ocrResult.Regions)
            {
                var left = word.Rect.BoundingRect().Left;
                var top = word.Rect.BoundingRect().Top;
                var right = word.Rect.BoundingRect().Right;
                var bottom = word.Rect.BoundingRect().Bottom;
                var wordRect = new Rect(left, top, right - left, bottom - top);
                TextRect textRect = new TextRect(wordRect, word.Text);
                textRects.Add(textRect);
            }

            return textRects;
        }



        public static List<TextRect> P_MaskTexts(string imageFile, string tempDir)
        {
            using PaddleOcrAll all = new(LocalFullModels.ChineseV3);
            all.Detector.UnclipRatio = 0.5f;
            using Mat srcImage = Cv2.ImRead(imageFile);
            var ocrResult = all.Run(srcImage);

            var textRects = new List<TextRect>();
            foreach (var word in ocrResult.Regions)
            {
                var left = word.Rect.BoundingRect().Left;
                var top = word.Rect.BoundingRect().Top;
                var right = word.Rect.BoundingRect().Right;
                var bottom = word.Rect.BoundingRect().Bottom;
                var wordRect = new Rect(left, top, right - left, bottom - top);
                TextRect textRect = new TextRect(wordRect, word.Text);
                textRects.Add(textRect);
            }

            return textRects;
        }

        public static string PaddleOCRText(string imageFile)
        {
            using Mat src = Cv2.ImRead(imageFile);
            Task<PaddleOcrResult> ocrResultTask = Task.Run(() =>
            {
                using PaddleOcrAll all = new(LocalFullModels.ChineseV3);
                all.Detector.UnclipRatio = 1.2f;
                return all.Run(src);
            });
            PaddleOcrResult ocrResult = ocrResultTask.Result;

            var pageTextCells = ocrResult.Regions.Select(word =>
            {
                return word.Text;
            }).ToList();

            return string.Join(" ", pageTextCells);
        }

        public static List<Cell> TesseractOCR(string imageFile)
        {
            var wordList = PDFDict.SDK.Sharp.Core.OCR.TesseractOCR.OCRWordLevel(imageFile);

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

            return pageTextCells;
        }

        public static List<TextRect> T_MaskTexts(Mat srcImage, string tempDir)
        {
            string path = Path.Combine(tempDir, $"{Guid.NewGuid().ToString()}.png");

            Cv2.ImWrite(path, srcImage);
            var wordList = PDFDict.SDK.Sharp.Core.OCR.TesseractOCR.OCRWordLevel(path);

            var textRects = new List<TextRect>();
            foreach (var word in wordList)
            {
                var left = (int)Math.Round(word.BBox.Left);
                var top = (int)Math.Round(word.BBox.Top);
                var right = (int)Math.Round(word.BBox.Right);
                var bottom = (int)Math.Round(word.BBox.Bottom);
                var wordRect = new Rect(left, top, right - left, bottom - top);
                TextRect textRect = new TextRect(wordRect, word.Text);
                textRects.Add(textRect);
            }

            return textRects;
        }
    }
}
