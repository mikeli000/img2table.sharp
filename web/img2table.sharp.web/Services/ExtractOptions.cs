namespace img2table.sharp.web.Services
{
    public class ExtractOptions
    {
        public static readonly string TempFolderName = "image2table_9966acf1-c43b-465c-bf7f-dd3c30394676";
        public static float DEFAULT_TEXT_OVERLAP_RATIO = 0.7f;
        public static float DEFAULT_IMAGE_OVERLAP_RATIO = 0.9f;

        public static float DEFAULT_RENDER_RESOLUTION = 300f;

        public static float PREDICT_CONFIDENCE_THRESHOLD = 0.5f;

        public bool UseEmbeddedHtml { get; set; } = false;
        public bool IgnoreMarginalia { get; set; } = false;
        public bool EnableOCR { get; set; } = false;
        public bool EmbedImagesAsBase64 { get; set; } = false;
        public bool OutputFigureAsImage { get; set; } = false;
        public string DocCategory { get; set; } = LayoutDetectorFactory.DocumentCategory.PPDocLayoutPlusL;
    }

    public class ExtractDebugOptions
    {
        public static bool _debug_draw_page_chunks = false;
        public static bool _debug_draw_text_box = false;
        public static bool _debug_save_dectect_image = true;
    }
}
