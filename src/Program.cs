using Img2table.Sharp.Img2table.Sharp.Data;
using Img2table.Sharp.Tabular;
using Img2table.Sharp.Tabular.TableElement;
using OpenCvSharp;

namespace Img2table.Sharp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var tempFile = @"C:/temp/img2table_data/borderless/i.png";
            Console.WriteLine(tempFile);

            using var img = new Mat(tempFile, ImreadModes.Color);
            var tableImage = new TableImage(img);
            List<Table> tables = tableImage.ExtractTables(false, false, true);
            foreach (var t in tables)
            {
                Console.WriteLine(t.ToString());
            }

            string json = TableJson.Serialize(tables);
            tables = TableJson.Deserialize(json);

            DrawTables(img, tables);

            using (new Window("dst image", img))
            Cv2.WaitKey();
        }

        static void DrawTables(Mat img, List<Table> tables)
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

            string outputPath = @"C:/temp/img2table_data/borderless/temp.png";
            Cv2.ImWrite(outputPath, img);
        }
    }
}
