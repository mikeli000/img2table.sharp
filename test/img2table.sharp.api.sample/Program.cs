using PDFDict.SDK.Sharp.Core;

namespace img2table.sharp.api.sample
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Start pdf content extracting...");

            //string pdfPath = @"C:\dev\testfiles\ai_testsuite\pdf\Cracking Test Data\test_report\Factsheet-BrandywineGLOBAL-AlternativeCreditFund-91103-FF-US-en-US\Factsheet-BrandywineGLOBAL-AlternativeCreditFund-91103-FF-US-en-US.pdf";
            //string pdfPath = @"C:\dev\testfiles\ai_testsuite\pdf\table\kv-test\6_CMS 3.0 Introduction.PDF";
            //await RunPDFAsync(pdfPath);
            //Console.WriteLine($"Processing file: {pdfPath}");

            await RunAllTestCase();

            Console.WriteLine("All done.");
        }

        public static async Task RunAllTestCase()
        {
            var rootFolder = @"C:\dev\testfiles\ai_testsuite\pdf\Cracking Test Data\test_report";

            var subfolders = Directory.GetDirectories(rootFolder);
            foreach (var folder in subfolders)
            {
                string caseName = Path.GetFileName(folder);

                Console.WriteLine($"Processing case: {caseName}");

                var pdfFile = Directory.GetFiles(folder, $"{caseName}.pdf");
                if (pdfFile.Length == 0)
                {
                    Console.WriteLine($"No PDF file found for case: {caseName}");
                    continue;
                }

                await RunPDFAsync(pdfFile[0]);
                Console.WriteLine($"Completed case: {caseName}");
            }

            Console.WriteLine("All test cases completed.");
        }

        public static async Task RunPDFAsync(string pdfPath)
        {
            var filebytes = File.ReadAllBytes(pdfPath);
            var folder = Path.GetDirectoryName(pdfPath);
            string fileName = Path.GetFileName(pdfPath);
            var documentChunks = await ExtractAPISample.ExtractAsync(filebytes, fileName, embedImagesAsBase64: true);

            string mdFile = "ccp-" + Path.ChangeExtension(fileName, ".md");
            string mdPath = Path.Combine(folder, mdFile);

            string md = documentChunks.Markdown;
            File.WriteAllText(mdPath, md);
        }
    }
}
