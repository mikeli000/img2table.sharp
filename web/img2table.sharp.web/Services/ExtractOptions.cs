namespace img2table.sharp.web.Services
{
    public class ExtractOptions
    {
        public static readonly string TempFolderName = "image2table_9966acf1-c43b-465c-bf7f-dd3c30394676";
        public static float DEFAULT_TEXT_OVERLAP_RATIO = 0.7f;
        public static float DEFAULT_IMAGE_OVERLAP_RATIO = 0.9f;

        public bool UseEmbeddedHtml { get; set; } = false;
        public bool IgnoreMarginalia { get; set; } = false;
        public bool EnableOCR { get; set; } = false;
        public bool EmbedImagesAsBase64 { get; set; } = false;
        public bool OutputFigureAsImage { get; set; } = false;
        public string DocCategory { get; set; } = LayoutDetectorFactory.DocumentCategory.AcademicPaper;
    }
}
