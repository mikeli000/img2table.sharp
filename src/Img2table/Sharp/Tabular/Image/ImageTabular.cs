using Img2table.Sharp.Tabular.TableElement;
using OpenCvSharp;

namespace Img2table.Sharp.Tabular.Image
{
    public class ImageTabular
    {
        public ImageTabular() { }

        public PagedTable Process(string imgFile)
        {
            if (string.IsNullOrWhiteSpace(imgFile) || !File.Exists(imgFile))
            {
                throw new FileNotFoundException("Image file not found", imgFile);
            }

            using var img = new Mat(imgFile, ImreadModes.Color);
            var tableImage = new TableImage(img);
            List<Table> tables = tableImage.ExtractTables(false, false, true);

            var pagedTable = new PagedTable
            {
                PageCount = 1,
                PageIndex = 0,
                PageImage = imgFile,
                Tables = tables,
            };

            return pagedTable;
        }
    }
}
