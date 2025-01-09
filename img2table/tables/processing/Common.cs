using img2table.sharp.img2table.tables.objects;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.img2table.tables.processing
{
    public class Common
    {
        public static bool is_contained_cell(Cell innerCell, Cell outerCell, double percentage = 0.9)
        {
            // 计算公共坐标
            int xLeft = Math.Max(innerCell.X1, outerCell.X1);
            int yTop = Math.Max(innerCell.Y1, outerCell.Y1);
            int xRight = Math.Min(innerCell.X2, outerCell.X2);
            int yBottom = Math.Min(innerCell.Y2, outerCell.Y2);

            // 计算交集面积和内单元格面积
            int intersectionArea = Math.Max(0, xRight - xLeft) * Math.Max(0, yBottom - yTop);

            return (double)intersectionArea / innerCell.Area >= percentage;
        }

        static List<Cell> merge_overlapping_contours(List<Cell> contours)
        {
            if (contours.Count == 0)
            {
                return new List<Cell>();
            }

            // 创建包含轮廓的列表
            var dfCnt = contours.Select((c, idx) => new { Id = idx, c.X1, c.Y1, c.X2, c.Y2, Area = c.Area }).ToList();

            // 交叉连接
            var dfCross = from c1 in dfCnt
                          from c2 in dfCnt
                          where c1.Id != c2.Id && c1.Area <= c2.Area
                          select new { c1, c2 };

            // 计算轮廓之间的交集面积并识别最小轮廓是否重叠最大轮廓
            var dfCrossWithIntersection = dfCross.Select(c => new
            {
                c.c1,
                c.c2,
                Intersection = Math.Max(0, Math.Min(c.c1.X2, c.c2.X2) - Math.Max(c.c1.X1, c.c2.X1)) *
                               Math.Max(0, Math.Min(c.c1.Y2, c.c2.Y2) - Math.Max(c.c1.Y1, c.c2.Y1))
            }).ToList();

            var dfCrossWithOverlaps = dfCrossWithIntersection.Select(c => new
            {
                c.c1,
                c.c2,
                c.Intersection,
                Overlaps = (double)c.Intersection / c.c1.Area >= 0.25
            }).ToList();

            // 识别相关轮廓：没有轮廓重叠
            var deletedContours = dfCrossWithOverlaps.Where(c => c.Overlaps).Select(c => c.c1.Id).Distinct().ToList();

            var dfOverlap = dfCrossWithOverlaps.Where(c => c.Overlaps)
                                               .GroupBy(c => c.c2.Id)
                                               .Select(g => new
                                               {
                                                   Id = g.Key,
                                                   X1Overlap = g.Min(c => c.c1.X1),
                                                   X2Overlap = g.Max(c => c.c1.X2),
                                                   Y1Overlap = g.Min(c => c.c1.Y1),
                                                   Y2Overlap = g.Max(c => c.c1.Y2)
                                               }).ToList();

            var dfFinal = dfCnt.Where(c => !deletedContours.Contains(c.Id))
                               .GroupJoin(dfOverlap, c => c.Id, o => o.Id, (c, o) => new { c, o })
                               .SelectMany(co => co.o.DefaultIfEmpty(), (co, o) => new
                               {
                                   X1 = Math.Min(co.c.X1, o?.X1Overlap ?? co.c.X1),
                                   X2 = Math.Max(co.c.X2, o?.X2Overlap ?? co.c.X2),
                                   Y1 = Math.Min(co.c.Y1, o?.Y1Overlap ?? co.c.Y1),
                                   Y2 = Math.Max(co.c.Y2, o?.Y2Overlap ?? co.c.Y2)
                               }).ToList();

            // 将结果映射到单元格
            return dfFinal.Select(d => new Cell(d.X1, d.Y1, d.X2, d.Y2)).ToList();
        }

        static List<Cell> merge_contours(List<Cell> contours, bool? vertically = true)
        {
            if (contours.Count == 0)
            {
                return contours;
            }

            if (vertically == null)
            {
                return merge_overlapping_contours(contours);
            }

            // 定义用于合并轮廓的维度
            string idx1 = vertically == true ? "Y1" : "X1";
            string idx2 = vertically == true ? "Y2" : "X2";
            string sortIdx1 = vertically == true ? "X1" : "Y1";
            string sortIdx2 = vertically == true ? "X2" : "Y2";

            // 排序轮廓
            var sortedCnts = contours.OrderBy(cnt => cnt.GetType().GetProperty(idx1).GetValue(cnt))
                                     .ThenBy(cnt => cnt.GetType().GetProperty(idx2).GetValue(cnt))
                                     .ThenBy(cnt => cnt.GetType().GetProperty(sortIdx1).GetValue(cnt))
                                     .ToList();

            // 循环遍历轮廓并合并重叠的轮廓
            var seq = sortedCnts.GetEnumerator();
            var listCnts = new List<Cell> { seq.Current };
            while (seq.MoveNext())
            {
                var cnt = seq.Current;
                if ((int)cnt.GetType().GetProperty(idx1).GetValue(cnt) <= (int)listCnts.Last().GetType().GetProperty(idx2).GetValue(listCnts.Last()))
                {
                    // 更新当前轮廓坐标
                    listCnts.Last().GetType().GetProperty(idx2).SetValue(listCnts.Last(), Math.Max((int)listCnts.Last().GetType().GetProperty(idx2).GetValue(listCnts.Last()), (int)cnt.GetType().GetProperty(idx2).GetValue(cnt)));
                    listCnts.Last().GetType().GetProperty(sortIdx1).SetValue(listCnts.Last(), Math.Min((int)listCnts.Last().GetType().GetProperty(sortIdx1).GetValue(listCnts.Last()), (int)cnt.GetType().GetProperty(sortIdx1).GetValue(cnt)));
                    listCnts.Last().GetType().GetProperty(sortIdx2).SetValue(listCnts.Last(), Math.Max((int)listCnts.Last().GetType().GetProperty(sortIdx2).GetValue(listCnts.Last()), (int)cnt.GetType().GetProperty(sortIdx2).GetValue(cnt)));
                }
                else
                {
                    listCnts.Add(cnt);
                }
            }

            return listCnts;
        }

        static List<Cell> get_contours_cell(Mat img, Cell cell, int margin = 5, int blurSize = 9, int kernelSize = 15, bool? mergeVertically = true)
        {
            Mat gray = new Mat();
            Cv2.CvtColor(img, gray, ColorConversionCodes.RGB2GRAY);
            int height = gray.Rows;
            int width = gray.Cols;

            // 获取裁剪后的图像
            Mat croppedImg = new Mat(gray, new Rect(Math.Max(cell.X1 - margin, 0), Math.Max(cell.Y1 - margin, 0), Math.Min(cell.X2 + margin, width) - Math.Max(cell.X1 - margin, 0), Math.Min(cell.Y2 + margin, height) - Math.Max(cell.Y1 - margin, 0)));

            // 如果裁剪后的图像为空，则不执行任何操作
            if (croppedImg.Empty())
            {
                return new List<Cell>();
            }

            // 重新处理图像
            Mat blur = new Mat();
            Cv2.GaussianBlur(croppedImg, blur, new Size(blurSize, blurSize), 0);
            Mat thresh = new Mat();
            Cv2.AdaptiveThreshold(blur, thresh, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, 11, 30);

            // 膨胀以组合相邻的文本轮廓
            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(kernelSize, kernelSize));
            Mat dilate = new Mat();
            Cv2.Dilate(thresh, dilate, kernel, iterations: 4);

            // 查找轮廓，突出显示文本区域，并提取ROI
            Point[][] contours;
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(dilate, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            // 获取轮廓列表
            List<Cell> listCntsCell = new List<Cell>();
            foreach (var c in contours)
            {
                Rect rect = Cv2.BoundingRect(c);
                int x = rect.X + cell.X1 - margin;
                int y = rect.Y + cell.Y1 - margin;
                Cell contourCell = new Cell(x, y, x + rect.Width, y + rect.Height);
                listCntsCell.Add(contourCell);
            }

            // 合并轮廓
            List<Cell> mergedContours = merge_contours(listCntsCell, mergeVertically);

            return mergedContours;
        }
    }
}
