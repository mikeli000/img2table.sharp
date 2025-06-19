using System;
using System.Collections;
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
            else if (string.Equals(label, Picture, StringComparison.OrdinalIgnoreCase))
            {
                return Picture;
            }
            else if (string.Equals(label, SectionHeader, StringComparison.OrdinalIgnoreCase))
            {
                return SectionHeader;
            }
            else if (string.Equals(label, Table, StringComparison.OrdinalIgnoreCase))
            {
                return Table;
            }
            else if (string.Equals(label, Text, StringComparison.OrdinalIgnoreCase))
            {
                return Text;
            }
            else if (string.Equals(label, Title, StringComparison.OrdinalIgnoreCase))
            {
                return Title;
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
        };
    }

    public static class ChunkUtils
    {
        public static bool IsOverlapping(double[] boxA, double[] boxB, double iouThreshold = 0.8)
        {
            // box: [x1, y1, x2, y2]
            double xA = Math.Max(boxA[0], boxB[0]);
            double yA = Math.Max(boxA[1], boxB[1]);
            double xB = Math.Min(boxA[2], boxB[2]);
            double yB = Math.Min(boxA[3], boxB[3]);

            double interWidth = Math.Max(0, xB - xA);
            double interHeight = Math.Max(0, yB - yA);
            double interArea = interWidth * interHeight;

            double boxAArea = (boxA[2] - boxA[0]) * (boxA[3] - boxA[1]);
            double boxBArea = (boxB[2] - boxB[0]) * (boxB[3] - boxB[1]);

            double iou = interArea / (boxAArea + boxBArea - interArea);

            return iou > iouThreshold;
        }

        public static List<ChunkObject> FilterOverlapping(IEnumerable<ChunkObject> objects, double iouThreshold = 0.8)
        {
            var result = new List<ChunkObject>();

            foreach (var obj in objects)
            {
                var overlapping = result.FirstOrDefault(existing =>
                    IsOverlapping(existing.BoundingBox, obj.BoundingBox, iouThreshold));

                if (overlapping == null)
                {
                    result.Add(obj);
                }
                else
                {
                    if ((obj.Confidence ?? 0) > (overlapping.Confidence ?? 0))
                    {
                        result.Remove(overlapping);
                        result.Add(obj);
                    }
                }
            }

            return result
                .OrderBy(o => o.BoundingBox[1])  // y1
                .ThenBy(o => o.BoundingBox[0])   // x1
                .ToList();
        }
    }
}
