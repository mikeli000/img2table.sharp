using img2table.sharp.web.Models;
using PDFDict.SDK.Sharp.Core.Contents;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using System.IO;
using Img2table.Sharp.Tabular;
using OpenCvSharp.LineDescriptor;

namespace img2table.sharp.web.Services
{
    public class ChunkElementProcessor
    {
        public static double DowngradeToTextConfidence = 0.4;

        private MarkdownWriter _writer;
        private bool _userEmbeddedHtml;
        private bool _ignoreMarginalia;

        public ChunkElementProcessor(bool userEmbeddedHtml = false, bool ignoreMarginalia = false)
        {
            _userEmbeddedHtml = userEmbeddedHtml;
            _ignoreMarginalia = ignoreMarginalia;
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
                case ChunkType.Abandon:
                    if (!_ignoreMarginalia) 
                    {
                        ProcessTextChunk(chunkElement);
                    }
                    break;
                case ChunkType.PlainText:
                    ProcessTextChunk(chunkElement);
                    break;
                case ChunkType.TableCaption:
                    _writer.WriteTitleTag(4);
                    WriteText(chunkElement);
                    _writer.AppendLine();
                    break;
                case ChunkType.TableFootnote:
                    _writer.WriteTitleTag(5);
                    WriteText(chunkElement);
                    _writer.AppendLine();
                    break;
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
                    if (!_ignoreMarginalia)
                    {
                        ProcessPageFooterChunk(chunkElement);
                    }
                    break;
                case ChunkType.PageHeader:
                    if (!_ignoreMarginalia)
                    {
                        ProcessPageHeaderChunk(chunkElement);
                    }
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
            //_writer.WriteListItemTag();
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
                    // TODO: handle image element
                }
                else if (content.PageElement is TextElement textElement)
                {
                    // TODO: group line
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

                    if (_userEmbeddedHtml && PDFTabular.TryBuildHTMLPiece(content.PageElement, out string html))
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

            TextElement prev = null;
            foreach (var content in contents)
            {
                if (content.PageElement == null)
                {
                    continue;
                }

                if (content.PageElement is not TextElement)
                {
                    // TODO
                    continue;
                }

                var curr = content.PageElement as TextElement;
                string text = curr.GetText();
                bool isListParagraphBegin = TextElement.IsListParagraphBegin(text);
                if (prev != null)
                {
                    if (Math.Round(prev.GetBaselineY()) == Math.Round(curr.GetBaselineY()))
                    {
                        isListParagraphBegin = false;
                    }
                }
                    
                if (_userEmbeddedHtml && PDFTabular.TryBuildHTMLPiece(content.PageElement, out string html))
                {
                    var newLineText = isListParagraphBegin ? "<br />" + html : html;
                    _writer.AppendText(newLineText);
                }
                else
                {
                    var newLineText = isListParagraphBegin ? "\n\n" + text : text;
                    _writer.AppendText(newLineText);
                }

                if (!isListParagraphBegin && TextElement.IsSpaceBetween(prev, curr))
                {
                    if (!_writer.IsEndWithSpace())
                    {
                        _writer.AppendText(" ");
                    }
                }

                prev = content.PageElement as TextElement;
            }
        }
    }

    public class MarkdownWriter
    {
        private readonly StringBuilder _sb = new StringBuilder();

        public bool IsEndWithSpace()
        {
            if (_sb == null || _sb.Length == 0)
            {
                return false;
            }
            
            char lastChar = _sb[_sb.Length - 1];
            return lastChar == ' ' || lastChar == '\n';
        }

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

}
