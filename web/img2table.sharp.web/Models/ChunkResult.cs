using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json.Serialization;

namespace img2table.sharp.web.Models
{
    public class ChunkResult
    {
        [JsonPropertyName("results")]
        public IEnumerable<PageChunk> Results { get; set; }
    }

    public class PageChunk
    {
        [JsonPropertyName("page")]
        public int? Page { get; set; }

        [JsonPropertyName("objects")]
        public IEnumerable<ChunkObject> Objects { get; set; }

        [JsonPropertyName("image_base64")]
        public string LabeledImage { get; set; }
    }

    public class ChunkObject
    {
        [JsonPropertyName("label")]
        public string Label { get; set; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; set; }

        [JsonPropertyName("bbox")]
        public double[] BoundingBox { get; set; }

        [JsonIgnore]
        public double X0 => BoundingBox[0];
        [JsonIgnore]
        public double Y0 => BoundingBox[1];
        [JsonIgnore]
        public double X1 => BoundingBox[2];
        [JsonIgnore]
        public double Y1 => BoundingBox[3];
    }

    public class ChunkType
    {
        //{0: 'Caption', 1: 'Footnote', 2: 'Formula', 3: 'List-item', 4: 'Page-footer', 5: 'Page-header', 6: 'Picture', 7: 'Section-header', 8: 'Table', 9: 'Text', 10: 'Title'}
        public const string Caption = "Caption";
        public const string Footnote = "Footnote";
        public const string Formula = "Formula";
        public const string ListItem = "List-item";
        public const string PageFooter = "Page-footer";
        public const string PageHeader = "Page-header";
        public const string Picture = "Picture";
        public const string SectionHeader = "Section-header";
        public const string Table = "Table";
        public const string Text = "Text";
        public const string Title = "Title";

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
        public const string Abandon = "abandon";
        public const string Figure = "figure";
        public const string FigureCaption = "figure_caption";
        public const string TableCaption = "table_caption";
        public const string TableFootnote = "table_footnote";
        public const string IsolateFormula = "isolate_formula";
        public const string FormulaCaption = "formula_caption";

        public const string Unknown = "Unknown";

        public static string MappingChunkType(string label)
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
            else if (string.Equals(label, PageFooter, StringComparison.OrdinalIgnoreCase))
            {
                return PageFooter;
            }
            else if (string.Equals(label, PageHeader, StringComparison.OrdinalIgnoreCase))
            {
                return PageHeader;
            }
            else if (string.Equals(label, Picture, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, Figure, StringComparison.OrdinalIgnoreCase))
            {
                return Picture;
            }
            else if (string.Equals(label, FigureCaption, StringComparison.OrdinalIgnoreCase))
            {
                return FigureCaption;
            }
            else if (string.Equals(label, SectionHeader, StringComparison.OrdinalIgnoreCase))
            {
                return SectionHeader;
            }
            else if (string.Equals(label, Table, StringComparison.OrdinalIgnoreCase))
            {
                return Table;
            }
            else if (string.Equals(label, TableCaption, StringComparison.OrdinalIgnoreCase))
            {
                return TableCaption;
            }
            else if (string.Equals(label, TableFootnote, StringComparison.OrdinalIgnoreCase))
            {
                return TableFootnote;
            }
            else if (string.Equals(label, Text, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, PlainText, StringComparison.OrdinalIgnoreCase))
            {
                return Text;
            }
            else if (string.Equals(label, Title, StringComparison.OrdinalIgnoreCase))
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
            else if (string.Equals(label, FormulaCaption, StringComparison.OrdinalIgnoreCase))
            {
                return FormulaCaption;
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
