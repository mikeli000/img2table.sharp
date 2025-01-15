using Img2table.Sharp.Tabular.TableImage.TableElement;
using OpenCvSharp;

namespace Img2table.Sharp.Tabular.TableImage.Processing.BorderedTables
{
    public class LineDetector
    {
        public static (List<Line>, List<Line>) DetectLines(Mat img, List<Cell> contours, double charLength, double minLineLength)
        {
            Mat blur = new Mat();
            Cv2.BilateralFilter(img, blur, 3, 40, 80);
            Mat gray = new Mat();
            Cv2.CvtColor(blur, gray, ColorConversionCodes.RGB2GRAY);

            Mat laplacian = new Mat();
            Cv2.Laplacian(gray, laplacian, MatType.CV_64F, ksize: 3);
            Mat edgeImg = new Mat();
            Cv2.ConvertScaleAbs(laplacian, edgeImg);

            foreach (var c in contours)
            {
                Rect rect = new Rect(c.X1 - 1, c.Y1 - 1, c.X2 - c.X1 + 2, c.Y2 - c.Y1 + 2);
                Cv2.Rectangle(edgeImg, rect, new Scalar(0), -1);
            }
            double meanEdge = Cv2.Mean(edgeImg).Val0;
            Cv2.MinMaxLoc(edgeImg, out _, out double maxEdge);
            Mat binaryImg = new Mat();
            Cv2.Threshold(edgeImg, binaryImg, Math.Min(2.5 * meanEdge, maxEdge), 255, ThresholdTypes.Binary);

            List<Line> hLines = IdentifyStraightLines(binaryImg, minLineLength, charLength, vertical: false);
            List<Line> vLines = IdentifyStraightLines(binaryImg, minLineLength, charLength, vertical: true);

            return (hLines, vLines);
        }

        private static List<Line> IdentifyStraightLines(Mat thresh, double minLineLength, double charLength, bool vertical)
        {
            Size kernelDims = vertical ? new Size(1, (int)Math.Round(minLineLength / 3) > 0 ? (int)Math.Round(minLineLength / 3) : 1)
                                       : new Size((int)Math.Round(minLineLength / 3) > 0 ? (int)Math.Round(minLineLength / 3) : 1, 1);
            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, kernelDims);
            Mat mask = new Mat();
            Cv2.MorphologyEx(thresh, mask, MorphTypes.Open, kernel, iterations: 1);

            Size hollowKernelDims = vertical ? new Size(3, 1) : new Size(1, 3);
            Mat hollowKernel = Cv2.GetStructuringElement(MorphShapes.Rect, hollowKernelDims);
            Mat maskClosed = new Mat();
            Cv2.MorphologyEx(mask, maskClosed, MorphTypes.Close, hollowKernel);

            Size dottedKernelDims = vertical ? new Size(1, (int)Math.Round(minLineLength / 6) > 0 ? (int)Math.Round(minLineLength / 6) : 1)
                                             : new Size((int)Math.Round(minLineLength / 6) > 0 ? (int)Math.Round(minLineLength / 6) : 1, 1);
            Mat dottedKernel = Cv2.GetStructuringElement(MorphShapes.Rect, dottedKernelDims);
            Mat maskDotted = new Mat();
            Cv2.MorphologyEx(maskClosed, maskDotted, MorphTypes.Close, dottedKernel);

            Size finalKernelDims = vertical ? new Size(1, (int)minLineLength > 0 ? (int)minLineLength : 1)
                                            : new Size((int)minLineLength > 0 ? (int)minLineLength : 1, 1);
            Mat finalKernel = Cv2.GetStructuringElement(MorphShapes.Rect, finalKernelDims);
            Mat finalMask = new Mat();
            Cv2.MorphologyEx(maskDotted, finalMask, MorphTypes.Open, finalKernel, iterations: 1);

            Mat labels = new Mat();
            Mat stats = new Mat();
            Mat centroids = new Mat();
            Cv2.ConnectedComponentsWithStats(finalMask, labels, stats, centroids, PixelConnectivity.Connectivity8, MatType.CV_32S);

            List<Line> lines = new List<Line>();
            for (int idx = 1; idx < stats.Rows; idx++)
            {
                int x = stats.At<int>(idx, 0);
                int y = stats.At<int>(idx, 1);
                int w = stats.At<int>(idx, 2);
                int h = stats.At<int>(idx, 3);
                int area = stats.At<int>(idx, 4);

                if (Math.Max(w, h) / (double)Math.Min(w, h) < 5 && Math.Min(w, h) >= charLength)
                {
                    continue;
                }

                if (Math.Max(w, h) < minLineLength)
                {
                    continue;
                }

                Mat cropped = new Mat(thresh, new Rect(x, y, w, h));
                if (w >= h)
                {
                    Mat sumCols = cropped.Reduce(ReduceDimension.Row, ReduceTypes.Sum, MatType.CV_32S);
                    Mat nonBlankPixels = new Mat();
                    Cv2.FindNonZero(sumCols.GreaterThan(0), nonBlankPixels);
                    Mat sumRows = cropped.Reduce(ReduceDimension.Column, ReduceTypes.Sum, MatType.CV_32S);
                    Mat lineRows = new Mat();
                    Cv2.FindNonZero(sumRows.Divide(255).ToMat().GreaterThanOrEqual(0.5 * w), lineRows);

                    if (lineRows.Rows == 0)
                    {
                        continue;
                    }

                    var d = (int)Math.Round(lineRows.Mean().Val1);
                    Line line = new Line(
                        x1: x + nonBlankPixels.At<Point>(0).X,
                        y1: y + d,
                        x2: x + nonBlankPixels.At<Point>(nonBlankPixels.Rows - 1).X,
                        y2: y + d,
                        thickness: lineRows.At<Point>(lineRows.Rows - 1).Y - lineRows.At<Point>(0).Y + 1
                    );
                    lines.Add(line);
                }
                else
                {
                    Mat sumRows = cropped.Reduce(ReduceDimension.Column, ReduceTypes.Sum, MatType.CV_32S);
                    Mat nonBlankPixels = new Mat();
                    Cv2.FindNonZero(sumRows.GreaterThan(0), nonBlankPixels);
                    Mat sumCols = cropped.Reduce(ReduceDimension.Row, ReduceTypes.Sum, MatType.CV_32S);
                    Mat lineCols = new Mat();
                    Cv2.FindNonZero(sumCols.Divide(255).ToMat().GreaterThanOrEqual(0.5 * h), lineCols);

                    if (lineCols.Rows == 0)
                    {
                        continue;
                    }

                    var meanLineCol = (int)Math.Round(lineCols.Mean().Val0);
                    Line line = new Line(
                        x1: x + meanLineCol,
                        y1: y + nonBlankPixels.At<Point>(0).Y,
                        x2: x + meanLineCol,
                        y2: y + nonBlankPixels.At<Point>(nonBlankPixels.Rows - 1).Y,
                        thickness: lineCols.At<Point>(lineCols.Rows - 1).X - lineCols.At<Point>(0).X + 1
                    );
                    lines.Add(line);
                }
            }

            return lines;
        }
    }
}
