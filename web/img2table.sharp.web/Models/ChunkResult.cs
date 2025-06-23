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

            return result;
        }

        public static List<ChunkObject> FilterContainment(IEnumerable<ChunkObject> objects)
        {
            var result = new List<ChunkObject>();

            var sorted = objects.OrderByDescending(obj => Area(obj.BoundingBox));
            foreach (var obj in sorted)
            {
                bool isContained = result.Any(existing =>
                    IsContainment(existing.BoundingBox, obj.BoundingBox));

                if (!isContained)
                {
                    result.Add(obj);
                }
            }

            return result;
        }

        public static bool IsContainment(double[] a, double[] b, double epsilon = 4.0)
        {
            return a[0] <= b[0] + epsilon &&
                   a[1] <= b[1] + epsilon &&
                   a[2] >= b[2] - epsilon &&
                   a[3] >= b[3] - epsilon;  
        }

        public static double Area(double[] rect)
        {
            var width = rect[2] - rect[0];
            var height = rect[3] - rect[1];
            return width * height;
        }

        public static List<ChunkObject> RebuildReadingOrder(IEnumerable<ChunkObject> objects)
        {
            var headers = objects.Where(c => c.Label == ChunkType.PageHeader).ToList().OrderBy(c => c.X1);
            var footers = objects.Where(c => c.Label == ChunkType.PageFooter).ToList().OrderBy(c => c.X1);
            var body = objects.Except(headers).Except(footers).ToList().OrderBy(c => c.Y1);

            var result = new List<ChunkObject>();

            result.AddRange(headers);
            result.AddRange(body);
            result.AddRange(footers);

            return result;
        }

        public static void SplitIntoColumns(List<ChunkObject> boxes, out List<ChunkObject> left, out List<ChunkObject> right)
        {
            var centers = boxes
                .Select(b => (b.X0 + b.X1) / 2)
                .OrderBy(c => c)
                .ToList();

            double maxGap = 0;
            double splitX = 0;

            for (int i = 0; i < centers.Count - 1; i++)
            {
                var gap = centers[i + 1] - centers[i];
                if (gap > maxGap)
                {
                    maxGap = gap;
                    splitX = (centers[i + 1] + centers[i]) / 2;
                }
            }

            left = SortByReadingOrder(boxes.Where(b => (b.X0 + b.X1) / 2 <= splitX).ToList());
            right = SortByReadingOrder(boxes.Where(b => (b.X0 + b.X1) / 2 > splitX).ToList());
        }

        public static List<ChunkObject> SortByReadingOrder(List<ChunkObject> boxes)
        {
            return boxes
                .OrderBy(b => b.Y0)
                .ThenBy(b => b.X0)
                .ToList();
        }
    }
}
