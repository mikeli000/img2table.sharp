using img2table.sharp.web.Models;
using PDFDict.SDK.Sharp.Core.Contents;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using System.IO;

namespace img2table.sharp.web.Controllers
{
    public class ChunkElementProcessor
    {
        public static double DowngradeToTextConfidence = 0.4;

        private MarkdownWriter _writer;
        private bool _userEmbeddedHtml;

        public ChunkElementProcessor(bool userEmbeddedHtml = false)
        {
            _userEmbeddedHtml = userEmbeddedHtml;
        }

        public string Process(ChunkElement chunkElement)
        {
            var chunkType = ChunkType.MappingChunkType(chunkElement.ChunkObject.Label);
            if (chunkType == ChunkType.Unknown)
            {
                return "";
            }

            _writer = new MarkdownWriter();
            switch (chunkType)
            {
                case ChunkType.Table:
                    ProcessTableChunk(chunkElement);
                    break;
                case ChunkType.Picture:
                    ProcessPictureChunk(chunkElement);
                    break;
                case ChunkType.SectionHeader:
                    ProcessSectionHeaderChunk(chunkElement);
                    break;
                case ChunkType.Caption:
                    ProcessCaptionChunk(chunkElement);
                    break;
                case ChunkType.Footnote:
                    ProcessFootnoteChunk(chunkElement);
                    break;
                case ChunkType.Formula:
                    ProcessFormulaChunk(chunkElement);
                    break;
                case ChunkType.ListItem:
                    ProcessListItemChunk(chunkElement);
                    break;
                case ChunkType.PageFooter:
                    ProcessPageFooterChunk(chunkElement);
                    break;
                case ChunkType.PageHeader:
                    ProcessPageHeaderChunk(chunkElement);
                    break;
                case ChunkType.Text:
                    ProcessTextChunk(chunkElement);
                    break;
                case ChunkType.Title:
                    ProcessTitleChunk(chunkElement);
                    break;
                default:
                    break;
            }

            chunkElement.MarkdownText = _writer.GetContent();
            return chunkElement.MarkdownText;
        }

        private void ProcessTitleChunk(ChunkElement chunkElement)
        {
            _writer.WriteTitleTag();
            WriteText(chunkElement);
            _writer.AppendLine();
        }

        private void ProcessTextChunk(ChunkElement chunkElement)
        {
            WriteText(chunkElement);
            _writer.AppendLine();
        }

        private void ProcessPageHeaderChunk(ChunkElement chunkElement)
        {
            _writer.WriteTitleTag(3);
            WriteText(chunkElement);
            _writer.AppendLine();
        }

        private void ProcessSectionHeaderChunk(ChunkElement chunkElement)
        {
            _writer.WriteTitleTag(2);
            WriteText(chunkElement);
            _writer.AppendLine();
        }
        
        private void ProcessListItemChunk(ChunkElement chunkElement)
        {
            var contents = chunkElement.ContentElements;
            if (contents == null || contents.Count() == 0)
            {
                return;
            }
            _writer.WriteListItemTag();
            WriteText(chunkElement);
            _writer.AppendLine();
        }

        private void ProcessFormulaChunk(ChunkElement chunkElement)
        {
            ProcessTextChunk(chunkElement);
        }

        private void ProcessFootnoteChunk(ChunkElement chunkElement)
        {
            ProcessTextChunk(chunkElement);
        }

        private void ProcessCaptionChunk(ChunkElement chunkElement)
        {
            ProcessTextChunk(chunkElement);
        }

        private void ProcessPageFooterChunk(ChunkElement chunkElement)
        {
            ProcessTextChunk(chunkElement);
        }

        private void ProcessTableChunk(ChunkElement chunkElement)
        {
            var contents = chunkElement.ContentElements;
            if (contents == null || contents.Count() == 0)
            {
                return;
            }

            _writer.WriteText(chunkElement.MarkdownText);
        }

        private void ProcessPictureChunk(ChunkElement chunkElement)
        {
            var contents = chunkElement.ContentElements;
            if (contents == null || contents.Count() == 0)
            {
                return;
            }

            var textElements = new List<ContentElement>();
            foreach (var content in contents)
            {
                if (content.PageElement is ImageElement imageElement)
                {
                    // handle image element
                }
                else if (content.PageElement is TextElement textElement)
                {
                    textElements.Add(content);
                }
            }

            if (textElements.Count > 0)
            {
                foreach (var content in contents)
                {
                    if (content.PageElement == null)
                    {
                        continue;
                    }

                    if (_userEmbeddedHtml && TryBuildHTMLPiece(content.PageElement, out string html))
                    {
                        _writer.WriteText(html);
                    }
                    else if (content.PageElement is TextElement textElement)
                    {
                        _writer.WriteText(textElement.GetText());
                    }
                }
            }
        }

        private void WriteText(ChunkElement chunkElement)
        {
            var contents = chunkElement.ContentElements;
            if (contents == null || contents.Count() == 0)
            {
                return;
            }

            foreach (var content in contents)
            {
                if (content.PageElement == null)
                {
                    continue;
                }

                if (_userEmbeddedHtml && TryBuildHTMLPiece(content.PageElement, out string html))
                {
                    _writer.AppendText(html);
                }
                else if (content.PageElement is TextElement textElement)
                {
                    _writer.AppendText(textElement.GetText());
                }
            }
        }

        private bool TryBuildHTMLPiece(PageElement pageElement, out string html)
        {
            html = null;
            if (pageElement is not TextElement)
            {
                return false;
            }

            var textElement = (TextElement)pageElement;
            if (textElement.GetGState() != null)
            {
                string css = GraphicsStateToCssConverter.Convert(textElement.GetGState());
                if (string.IsNullOrWhiteSpace(css))
                {
                    return false;
                }

                html = $"<span style=\"{css}\">{textElement.GetText()}</span>";
                return true;
            }

            return false;
        }
    }

    public class MarkdownWriter
    {
        private readonly StringBuilder _sb = new StringBuilder();

        public void AppendText(string text)
        {
            _sb.Append(text);
        }

        public void AppendLine()
        {
            _sb.Append("\n");
        }

        public void WriteTitleTag(int level = 1)
        {
            level = Math.Clamp(level, 1, 6);
            _sb.Append($"{new string('#', level)} ");
        }

        public void WriteListItemTag()
        {
            _sb.Append("- ");
        }

        public void WriteText(string text)
        {
            _sb.AppendLine(text);
            _sb.AppendLine();
        }

        public void WriteTable(string[] headers, List<string[]> rows)
        {
            _sb.AppendLine("| " + string.Join(" | ", headers) + " |");
            _sb.AppendLine("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");

            foreach (var row in rows)
            {
                _sb.AppendLine("| " + string.Join(" | ", row) + " |");
            }
            _sb.AppendLine();
        }

        public void WritePicture(string imageUrl, string altText = "")
        {
            _sb.AppendLine($"![{altText}]({imageUrl})");
            _sb.AppendLine();
        }

        public void WriteListItem(IEnumerable<string> items, bool ordered = false)
        {
            int i = 1;
            foreach (var item in items)
            {
                _sb.AppendLine(ordered ? $"{i}. {item}" : $"- {item}");
                i++;
            }
            _sb.AppendLine();
        }

        public void WriteHtml(string html)
        {
            _sb.AppendLine(html);
        }

        public string GetContent()
        {
            return _sb.ToString();
        }

        public void SaveToFile(string path)
        {
            File.WriteAllText(path, GetContent());
        }
    }

    public static class GraphicsStateToCssConverter
    {
        public static string Convert(GraphicsState g)
        {
            if (g == null || g.TextState == null)
            { 
                return string.Empty; 
            } 

            var sb = new StringBuilder();
            if (g.NonStrokingColor != null)
            {
                var color = ConvertColor(g.NonStrokingColor);
                if (color != null)
                {
                    sb.Append($"color: {color};");
                }
            }

            if (g.TextState.FontSize > 0)
            {
                sb.Append($"font-size: {g.TextState.FontSize}pt;");
            }

            if (g.TextState.FontWeight >= 600)
            {
                sb.Append("font-weight: bold;");
            }

            if (Math.Abs(g.TextState.FontItalicAngle) > 0.1)
            {
                sb.Append("font-style: italic;");
            }

            return sb.ToString();
        }

        private static string ConvertColor(ColorState color)
        {
            if (color?.Components != null && color?.Components.Length == 4)
            {
                int r = (int)color.Components[0];
                int g = (int)color.Components[1];
                int b = (int)color.Components[2];
                return $"rgb({r},{g},{b})";
            }

            return null;
        }
    }

}
