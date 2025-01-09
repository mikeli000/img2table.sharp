using Img2table.Sharp.Core.Tables;
using Img2table.Sharp.Core.Tables.Objects;
using OpenCvSharp;
using System;
using Xunit;

namespace img2table.sharp.tests
{
    public class UnitTest
    {
        [Fact]
        public void TestImplicit()
        {
            string file = Path.Combine(Environment.CurrentDirectory, @"Files/Images/implicit.png");
            using var img = new Mat(file, ImreadModes.Color);
            var tableImage = new TableImage(img);
            List<Table> tables = tableImage.ExtractTables(false, false, true);
            foreach (var t in tables)
            {
                Console.WriteLine(t.ToString());
            }

            Assert.Equal(2, 2);
        }
    }
}