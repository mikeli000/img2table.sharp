using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json.Serialization;

namespace img2table.sharp.web.Models
{
    public class LayoutDetectionResult
    {
        [JsonPropertyName("results")]
        public IEnumerable<PageDetectionResult> Results { get; set; }
    }

    public class PageDetectionResult
    {
        [JsonPropertyName("page")]
        public int? Page { get; set; }

        [JsonPropertyName("objects")]
        public IEnumerable<ObjectDetectionResult> Objects { get; set; }

        [JsonPropertyName("image_base64")]
        public string LabeledImage { get; set; }
    }

    public class ObjectDetectionResult
    {
        [JsonPropertyName("label")]
        public string Label { get; set; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; set; }

        [JsonPropertyName("bbox")]
        public double[] BoundingBox { get; set; }

        [JsonIgnore]
        public IEnumerable<TableCellDetectionResult> Cells { get; set; }

        [JsonIgnore]
        public double X0 => BoundingBox[0];
        [JsonIgnore]
        public double Y0 => BoundingBox[1];
        [JsonIgnore]
        public double X1 => BoundingBox[2];
        [JsonIgnore]
        public double Y1 => BoundingBox[3];
        [JsonIgnore]
        public double Width => X1 - X0;
    }

    public class TableDetectionResult
    {
        public IEnumerable<TableRowDetectionResult> Rows { get; set; }
    }

    public class TableRowDetectionResult
    {
        public IEnumerable<TableCellDetectionResult> Cells { get; set; }
    }

    public class TableCellDetectionResult
    {
        public string Content { get; set; } = string.Empty;

        public int? RowSpan { get; set; } = 1;

        public int? ColSpan { get; set; } = 1;

        public int[] BoundingBox { get; set; }

        public int X0 => BoundingBox[0];
        public int Y0 => BoundingBox[1];
        public int X1 => BoundingBox[2];
        public int Y1 => BoundingBox[3];
    }

    public class DetectionLabel
    {
        //{0: 'Caption', 1: 'Footnote', 2: 'Formula', 3: 'List-item', 4: 'Page-footer', 5: 'Page-header', 6: 'Picture', 7: 'Section-header', 8: 'Table', 9: 'Text', 10: 'Title'}
        public const string Caption = "Caption";
        public const string Footnote = "Footnote";
        public const string Formula = "Formula";
        public const string Chart = "Chart";
        public const string ListItem = "List-item";
        public const string PageFooter = "Page-footer";
        public const string PageHeader = "Page-header";
        public const string Header = "header";
        public const string Footer = "footer";
        public const string Picture = "Picture";
        public const string SectionHeader = "Section-header";
        public const string Table = "Table";
        public const string Text = "Text";
        public const string Title = "Title";
        public const string DocTitle = "doc_title";
        public const string ParagraphTitle = "paragraph_title";

        /**
            label_map = {
                0: 'title',
                1: 'plain text',
                2: 'abandon',
                3: 'figure',
                4: 'figure_caption',
                5: 'table',
                6: 'table_caption',
                7: 'table_footnote',
                8: 'isolate_formula',
                9: 'formula_caption'
            }
         */
        public const string PlainText = "plain text";
        public const string Abstract = "abstract";
        public const string Abandon = "abandon";
        public const string AsideText = "aside_text";
        public const string Figure = "figure";
        public const string Image = "image";
        public const string FigureCaption = "figure_caption";
        public const string FigureTitle = "figure_title";
        public const string TableCaption = "table_caption";
        public const string TableFootnote = "table_footnote";
        public const string IsolateFormula = "isolate_formula";
        public const string FormulaCaption = "formula_caption";
        public const string Number = "number";
        public const string PageNumber = "page_number";
        public const string Content = "content";

        public const string Unknown = "Unknown";

        public static string NormalizeLabel(string label)
        {
            if (string.Equals(label, Caption, StringComparison.OrdinalIgnoreCase))
            {
                return Caption;
            }
            else if (string.Equals(label, Footnote, StringComparison.OrdinalIgnoreCase))
            {
                return Footnote;
            }
            else if (string.Equals(label, Formula, StringComparison.OrdinalIgnoreCase))
            {
                return Formula;
            }
            else if (string.Equals(label, ListItem, StringComparison.OrdinalIgnoreCase))
            {
                return ListItem;
            }
            else if (string.Equals(label, PageFooter, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, Footer, StringComparison.OrdinalIgnoreCase))
            {
                return PageFooter;
            }
            else if (string.Equals(label, PageHeader, StringComparison.OrdinalIgnoreCase) 
                || string.Equals(label, Header, StringComparison.OrdinalIgnoreCase))
            {
                return PageHeader;
            }
            else if (string.Equals(label, Picture, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, Figure, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, Image, StringComparison.OrdinalIgnoreCase))
            {
                return Picture;
            }
            else if (string.Equals(label, FigureCaption, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, FigureTitle, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, FormulaCaption, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, TableCaption, StringComparison.OrdinalIgnoreCase))
            {
                return Caption;
            }
            else if (string.Equals(label, SectionHeader, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, ParagraphTitle, StringComparison.OrdinalIgnoreCase))
            {
                return SectionHeader;
            }
            else if (string.Equals(label, Table, StringComparison.OrdinalIgnoreCase))
            {
                return Table;
            }
            else if (string.Equals(label, TableFootnote, StringComparison.OrdinalIgnoreCase))
            {
                return TableFootnote;
            }
            else if (string.Equals(label, Text, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, PlainText, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, Abstract, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, Content, StringComparison.OrdinalIgnoreCase))
            {
                return Text;
            }
            else if (string.Equals(label, AsideText, StringComparison.OrdinalIgnoreCase))
            {
                return Text;
            }
            else if (string.Equals(label, Number, StringComparison.OrdinalIgnoreCase))
            {
                return Number;
            }
            else if (string.Equals(label, Title, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, DocTitle, StringComparison.OrdinalIgnoreCase))
            {
                return Title;
            }
            else if (string.Equals(label, Abandon, StringComparison.OrdinalIgnoreCase))
            {
                return Abandon;
            }
            else if (string.Equals(label, IsolateFormula, StringComparison.OrdinalIgnoreCase))
            {
                return IsolateFormula;
            }
            else if (string.Equals(label, Chart, StringComparison.OrdinalIgnoreCase))
            {
                return Chart;
            }
            else
            {
                return Unknown;
            }
        }

        public static IDictionary<string, Color> LabelColors = new Dictionary<string, Color>
        {
            { Caption, Color.FromArgb(255, 0, 0) },
            { Footnote, Color.FromArgb(0, 255, 0) },
            { Formula, Color.FromArgb(0, 0, 255) },
            { ListItem, Color.FromArgb(255, 255, 0) },
            { PageFooter, Color.FromArgb(255, 0, 255) },
            { PageHeader, Color.FromArgb(0, 255, 255) },
            { Picture, Color.FromArgb(128, 0, 255) },
            { SectionHeader, Color.FromArgb(0, 128, 255) },
            { Table, Color.FromArgb(128, 255, 0) },
            { Text, Color.FromArgb(128, 128, 128) },
            { Title, Color.FromArgb(0, 0, 0) },
            
            { PlainText, Color.FromArgb(192, 192, 192) },
            { Abandon, Color.FromArgb(255, 128, 128) },
            { FigureCaption, Color.FromArgb(128, 0, 0) },
            { TableCaption, Color.FromArgb(0, 128, 0) },
            { TableFootnote, Color.FromArgb(0, 0, 128) },
            { IsolateFormula, Color.FromArgb(128, 128, 0) },
            { FormulaCaption, Color.FromArgb(128, 0, 128) },
            { Unknown, Color.FromArgb(255, 255, 255) }
        };
    }
}
