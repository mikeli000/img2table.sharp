using Img2table.Sharp.Tabular.TableImage.TableElement;
using OpenCvSharp;
using Sdcb.PaddleOCR.Models.Local;
using Sdcb.PaddleOCR;

namespace img2table.sharp.Img2table.Sharp.Tabular
{
    public class OCRUtils
    {
        public static List<Cell> PaddleOCR(string imageFile)
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
    }
}
