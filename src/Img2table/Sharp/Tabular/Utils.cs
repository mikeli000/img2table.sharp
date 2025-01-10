
using OpenCvSharp;

namespace Img2table.Sharp.Tabular
{
    public static class Utils
    {
        public static bool IsMatsEqual(Mat mat1, Mat mat2)
        {
            if (mat1.Size() != mat2.Size() || mat1.Type() != mat2.Type())
            {
                return false;
            }

            Mat diff = new Mat();
            Cv2.Compare(mat1, mat2, diff, CmpType.NE);

            int nonZeroCount = Cv2.CountNonZero(diff);
            return nonZeroCount == 0;
        }

        public static double CalculateMedian(Mat mat)
        {
            double[] array = ConvertMatToArray(mat);
            Array.Sort(array);

            // Calculate the median
            int mid = array.Length / 2;
            double median = array.Length % 2 != 0 ? array[mid] : (array[mid - 1] + array[mid]) / 2.0;

            return median;
        }

        public static double[] ConvertMatToArray(Mat mat)
        {
            int rows = mat.Rows;
            int cols = mat.Cols;
            double[] array = new double[rows * cols];
            int index = 0;

            switch (mat.Type().Depth)
            {
                case MatType.CV_8U:
                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            array[index++] = mat.At<byte>(i, j);
                        }
                    }
                    break;
                case MatType.CV_16U:
                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            array[index++] = mat.At<ushort>(i, j);
                        }
                    }
                    break;
                case MatType.CV_16S:
                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            array[index++] = mat.At<short>(i, j);
                        }
                    }
                    break;
                case MatType.CV_32S:
                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            array[index++] = mat.At<int>(i, j);
                        }
                    }
                    break;
                case MatType.CV_32F:
                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            array[index++] = mat.At<float>(i, j);
                        }
                    }
                    break;
                case MatType.CV_64F:
                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            array[index++] = mat.At<double>(i, j);
                        }
                    }
                    break;
                default:
                    throw new NotSupportedException("Unsupported Mat type");
            }

            return array;
        }

        public static double Median(double[] sourceNumbers)
        {
            if (sourceNumbers == null || sourceNumbers.Length == 0)
                throw new ArgumentException("Median of empty array not defined.");

            double[] sortedNumbers = (double[])sourceNumbers.Clone();
            Array.Sort(sortedNumbers);

            int size = sortedNumbers.Length;
            int mid = size / 2;
            double median = size % 2 != 0 ? sortedNumbers[mid] : (sortedNumbers[mid] + sortedNumbers[mid - 1]) / 2;
            return median;
        }

        public static Mat ListToMat(List<List<int>> list)
        {
            Mat mat = new Mat(list.Count, list[0].Count, MatType.CV_32S);
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = 0; j < list[i].Count; j++)
                {
                    mat.Set(i, j, list[i][j]);
                }
            }
            return mat;
        }

        public static int[] BinCount(int[] array)
        {
            int max = array.Max();
            int[] binCount = new int[max + 1];
            foreach (int value in array)
            {
                binCount[value]++;
            }
            return binCount;
        }

        public static int ArgMax(int[] array)
        {
            int maxIndex = 0;
            for (int i = 1; i < array.Length; i++)
            {
                if (array[i] > array[maxIndex])
                {
                    maxIndex = i;
                }
            }
            return maxIndex;
        }

        public static void PrintMat(Mat mat)
        {
            Console.Write("[");
            for (int row = 0; row < mat.Rows; row++)
            {
                if (row > 0)
                {
                    Console.Write(" ");
                }

                Console.Write("[");
                for (int col = 0; col < mat.Cols; col++)
                {
                    if (mat.Cols > 20 && col == 6)
                    {
                        Console.Write("... ");
                        col = mat.Cols - 7;
                        continue;
                    }

                    if (mat.Type() == MatType.CV_8U)
                    {
                        Console.Write(mat.At<byte>(row, col));
                    }
                    else if (mat.Type() == MatType.CV_32S)
                    {
                        Console.Write(mat.At<int>(row, col));
                    }
                    else if (mat.Type() == MatType.CV_32F)
                    {
                        Console.Write(mat.At<float>(row, col));
                    }
                    else if (mat.Type() == MatType.CV_64F)
                    {
                        Console.Write(mat.At<double>(row, col));
                    }
                    else
                    {
                        Console.Write("?");
                    }

                    if (col < mat.Cols - 1)
                    {
                        Console.Write(" ");
                    }
                }
                Console.Write("]");
                if (row < mat.Rows - 1)
                {
                    Console.WriteLine();
                }
            }
            Console.WriteLine("]");
        }

        public static void PrintDArray(double[,] array)
        {
            int rows = array.GetLength(0);
            int cols = array.GetLength(1);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    Console.Write(array[i, j] + " ");
                }
                Console.WriteLine();
            }
        }

        public static Mat CreateBinaryImage(Mat mat, int val = 1)
        {
            Mat rsla_img = new Mat(mat.Size(), MatType.CV_8UC1);

            for (int row = 0; row < mat.Rows; row++)
            {
                for (int col = 0; col < mat.Cols; col++)
                {
                    bool gt = false;
                    if (mat.Type() == MatType.CV_8U)
                    {
                        gt = mat.At<byte>(row, col) > 0;
                    }
                    else if (mat.Type() == MatType.CV_32S)
                    {
                        gt = mat.At<int>(row, col) > 0;
                    }
                    else if (mat.Type() == MatType.CV_32F)
                    {
                        gt = mat.At<float>(row, col) > 0;
                    }
                    else if (mat.Type() == MatType.CV_64F)
                    {
                        gt = mat.At<double>(row, col) > 0;
                    }
                    rsla_img.Set(row, col, gt ? val : 0);
                }
            }

            return rsla_img;
        }
    }
}
