using img2table.sharp.web.Models;
using PDFDict.SDK.Sharp.Core.Contents;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using System.IO;
using img2table.sharp.Img2table.Sharp.Tabular;
using static System.Net.Mime.MediaTypeNames;

namespace img2table.sharp.web.Services
{
    public class ChunkElementProcessor
    {
        public static double DowngradeToTextConfidence = 0.4;

        private MarkdownWriter _writer;
        private bool _userEmbeddedHtml;
        private bool _ignoreMarginalia;
        private bool _outputFigureAsImage;
        private string _workFolder;
        private string _jobFolderName;
        private string _pageImagePath;
        private bool _enableOCR = true;
        private bool _removeBulletChar = false;

        public ChunkElementProcessor(string workFolder, string jobFolderName, bool userEmbeddedHtml = false, bool ignoreMarginalia = false, bool outputFigureAsImage = false, bool enableOCR = false)
        {
            _userEmbeddedHtml = userEmbeddedHtml;
            _ignoreMarginalia = ignoreMarginalia;
            _outputFigureAsImage = outputFigureAsImage;
            _workFolder = workFolder ?? throw new ArgumentNullException(nameof(workFolder));
            _jobFolderName = jobFolderName ?? throw new ArgumentNullException(nameof(jobFolderName));
            _enableOCR = enableOCR;
        }

        public string Process(ChunkElement chunkElement, string pageImagePath)
        {
            _pageImagePath = pageImagePath;
            var chunkType = ChunkType.MappingChunkType(chunkElement.ChunkObject.Label);
            if (chunkType == ChunkType.Unknown)
            {
                return "";
            }
            chunkElement.ChunkObject.Label = chunkType;

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
                case ChunkType.TableFootnote:
                    _writer.WriteTitleTag(5);
                    WriteText(chunkElement);
                    _writer.AppendLine();
                    break;
                case ChunkType.Table:
                    ProcessTableChunk(chunkElement);
                    break;
                case ChunkType.Picture:
                case ChunkType.Figure:
                case ChunkType.Chart:
                    ProcessPictureChunk(chunkElement);
                    break;
                case ChunkType.SectionHeader:
                case ChunkType.ParagraphTitle:
                    ProcessSectionHeaderChunk(chunkElement);
                    break;
                case ChunkType.Caption:
                case ChunkType.TableCaption:
                case ChunkType.FigureCaption:
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
                case ChunkType.Number:
                case ChunkType.PageNumber:
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
            _writer.WriteTitleTag(4);
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
                    //if (this._outputFigureAsImage)
                    //{
                        //var imageName = $"{Guid.NewGuid().ToString()}.png";
                        //string imageFile = Path.Combine(_workFolder, imageName);

                        //var imagePath = $"{WorkDirectoryOptions.RequestPath}/{_jobFolderName}/{imageName}";
                        //imageElement.PDFImage.Save(imageFile);
                        //_writer.WritePicture(imagePath);
                    //}
                }
                else if (content.PageElement is TextElement textElement)
                {
                    textElements.Add(content);
                }
            }

            if (textElements.Count > 0)
            {
                WriteText(textElements, chunkElement.ChunkObject);
            }
            else
            {
                if (_outputFigureAsImage)
                {
                    var imageName = $"img_{Guid.NewGuid().ToString()}.png";
                    string tempImagePath = Path.Combine(_workFolder, imageName);
                    ChunkUtils.ClipChunkRectImage(_pageImagePath, tempImagePath, chunkElement.ChunkObject, false);

                    var imageUrl = $"{WorkDirectoryOptions.RequestPath}/{_jobFolderName}/{imageName}";
                    _writer.WritePicture(imageUrl);
                }
            }
        }

        private void WriteLine(IEnumerable<ContentElement> contents, ChunkObject chunkObject, bool autoOCR)
        {
            var lines = LineBreakProcessor.ProcessLineBreaks(contents, chunkObject, autoOCR, _workFolder, _pageImagePath);
            if (lines == null || lines.Count == 0)
            {
                return;
            }
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line.Text))
                {
                    _writer.AppendLine();
                }
                else
                {
                    bool isListParagraphBegin = TextElement.IsListParagraphBegin(line.Text, out var listTag);
                    var firstContent = line.contents.FirstOrDefault()?.PageElement;
                    if (firstContent != null && firstContent is TextElement)
                    {
                        isListParagraphBegin = ((TextElement)firstContent).IsWingdingFont();
                    }

                    if (isListParagraphBegin)
                    {
                        _writer.WriteListItemTag();
                    }
                    _writer.AppendText(line.Text);
                }
            }
        }

        private void WriteText(IEnumerable<ContentElement> contents, ChunkObject chunkObject, bool autoLinkBreak = false, bool autoOCR = false)
        {
            if (autoLinkBreak)
            {
                WriteLine(contents, chunkObject, autoOCR);
                return;
            }

            TextElement prev = null;
            foreach (var content in contents)
            {
                if (content.PageElement == null)
                {
                    continue;
                }

                if (content.PageElement is ImageElement)
                {
                    //var imageName = $"img_{Guid.NewGuid().ToString()}.png";
                    //string tableImagePath = Path.Combine(_workFolder, imageName);
                    //ChunkUtils.ClipTableImage(_pageImagePath, tableImagePath, chunkElement.ChunkObject);

                    //string text = OCRUtils.PaddleOCRText(tableImagePath);
                    //if (!string.IsNullOrWhiteSpace(text))
                    //{
                    //    _writer.AppendText(text);
                    //}
                }
                else if (content.PageElement is TextElement)
                {
                    var curr = content.PageElement as TextElement;
                    string text = curr.GetText();
                    bool isListParagraphBegin = TextElement.IsListParagraphBegin(text, out var listTag) || curr.IsWingdingFont();
                    if (prev != null)
                    {
                        if (Math.Round(prev.GetBaselineY()) == Math.Round(curr.GetBaselineY()))
                        {
                            isListParagraphBegin = false;
                        }
                    }

                    if (_userEmbeddedHtml && content.PageElement.TryBuildHTMLPiece(out string html))
                    {
                        string newLineText = html;
                        if (isListParagraphBegin)
                        {
                            newLineText = "<br />" + newLineText;
                        }
                        else
                        {
                            if (prev != null)
                            {
                                if (TextElement.IsSpaceBetween(prev, curr) || Math.Round(prev.GetBaselineY()) != Math.Round(curr.GetBaselineY()))
                                {
                                    newLineText = " " + newLineText;
                                }
                            }
                        }

                        _writer.AppendText(newLineText);
                    }
                    else
                    {
                        string newLineText = text;

                        if (isListParagraphBegin)
                        {
                            if (_removeBulletChar)
                            {
                                newLineText = newLineText.Substring(listTag == null ? 0 : listTag.Length);
                            }
                            newLineText = "\n\n" + "- " + newLineText;
                        }
                        else
                        {
                            if (prev != null)
                            {
                                if (TextElement.IsSpaceBetween(prev, curr) || Math.Round(prev.GetBaselineY()) != Math.Round(curr.GetBaselineY()))
                                {
                                    if (!_writer.IsEndWithSpace())
                                    {
                                        newLineText = " " + newLineText;
                                    }
                                }
                            }
                        }
                        _writer.AppendText(newLineText);
                    }

                    prev = curr;
                }
            }
        }

        private void WriteText(ChunkElement chunkElement)
        {
            var contents = chunkElement.ContentElements;
            bool tryOCR = false;
            if (contents == null || contents.Count() == 0)
            {
                tryOCR = true;
            }
            else if (contents.Count() == 1)
            {
                tryOCR = contents.ElementAt(0).PageElement is ImageElement;
            }

            if (tryOCR)
            {
                var imageName = $"img_{Guid.NewGuid().ToString()}.png";
                string tempImagePath = Path.Combine(_workFolder, imageName);
                ChunkUtils.ClipChunkRectImage(_pageImagePath, tempImagePath, chunkElement.ChunkObject, false);

                var text = OCRUtils.PaddleOCRText(tempImagePath);

                if (_enableOCR && !string.IsNullOrEmpty(text))
                {
                    _writer.AppendText(text);
                }
                else
                {
                    var imagePath = $"{WorkDirectoryOptions.RequestPath}/{_jobFolderName}/{imageName}";
                    _writer.WritePicture(imagePath);
                }

                return;
            }

            WriteText(contents, chunkElement.ChunkObject, true, _enableOCR);
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
