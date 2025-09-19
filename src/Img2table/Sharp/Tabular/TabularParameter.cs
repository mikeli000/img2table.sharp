namespace Img2table.Sharp.Tabular
{
    public class TabularParameter
    {
        public static float DEFAULT_OVERLAP_RATIO = 0.8f;

        public bool ImplicitRows { get; set; } = false;

        public bool ImplicitColumns { get; set; } = false;
        
        public bool DetectBorderlessTables { get; set; } = false;

        public float RenderResolution { get; set; } = 96f;

        public float CellTextOverlapRatio { get; set; } = DEFAULT_OVERLAP_RATIO;

        public float OCRCellTextOverlapRatio { get; set; } = 0.6f;

        public static TabularParameter Default = new TabularParameter 
        {
            ImplicitRows = false,
            ImplicitColumns = false,
            DetectBorderlessTables = false,
            RenderResolution = 96f,
            CellTextOverlapRatio = DEFAULT_OVERLAP_RATIO,
        };

        public static TabularParameter AutoDetect = new TabularParameter
        {
            ImplicitRows = false,
            ImplicitColumns = false,
            DetectBorderlessTables = true,
            RenderResolution = 144,
            CellTextOverlapRatio = DEFAULT_OVERLAP_RATIO,
        };
    }
}
