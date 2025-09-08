using img2table.sharp.web.Models;
using PDFDict.SDK.Sharp.Core.Contents;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using System.IO;
using img2table.sharp.Img2table.Sharp.Tabular;

namespace img2table.sharp.web.Services
{
    public class ChunkElementProcessor
    {
        private MarkdownWriter _writer;
        private bool _useEmbeddedHtml;
        private bool _ignoreMarginalia;
        private bool _outputFigureAsImage;
        private string _workFolder;
        private string _jobFolderName;
        private string _pageImagePath;
        private bool _enableOCR = true;
        private bool _removeBulletChar = false;
        private bool _embedBase64ImageData = false;

        public ChunkElementProcessor(string workFolder, string jobFolderName, ChunkElementProcessorParameter paras)
        {
            _workFolder = workFolder ?? throw new ArgumentNullException(nameof(workFolder));
            _jobFolderName = jobFolderName ?? throw new ArgumentNullException(nameof(jobFolderName));

            if (paras != null)
            {
                _useEmbeddedHtml = paras.UseEmbeddedHtml;
                _ignoreMarginalia = paras.IgnoreMarginalia;
                _outputFigureAsImage = paras.OutputFigureAsImage;
                _workFolder = workFolder ?? throw new ArgumentNullException(nameof(workFolder));
                _jobFolderName = jobFolderName ?? throw new ArgumentNullException(nameof(jobFolderName));
                _enableOCR = paras.EnableOCR;
                _removeBulletChar = paras.RemoveBulletChar;
                _embedBase64ImageData = paras.EmbedBase64ImageData;
            }
        }

        public string Process(ChunkElement chunkElement, string pageImagePath)
        {
            _pageImagePath = pageImagePath;
            var chunkType = DetectionLabel.NormalizeLabel(chunkElement.ChunkObject.Label);
            if (chunkType == DetectionLabel.Unknown)
            {
                return "";
            }
            chunkElement.ChunkObject.Label = chunkType;

            _writer = new MarkdownWriter();
            switch (chunkType)
            {
                case DetectionLabel.Abandon:
                    if (!_ignoreMarginalia) 
                    {
                        ProcessTextChunk(chunkElement);
                    }
                    break;
                case DetectionLabel.PlainText:
                    ProcessTextChunk(chunkElement);
                    break;
                case DetectionLabel.TableFootnote:
                    WriteText(chunkElement, 5);
                    _writer.AppendLine();
                    break;
                case DetectionLabel.Table:
                    ProcessTableChunk(chunkElement);
                    break;
                case DetectionLabel.Picture:
                case DetectionLabel.Figure:
                case DetectionLabel.Chart:
                    ProcessPictureChunk(chunkElement);
                    break;
                case DetectionLabel.SectionHeader:
                case DetectionLabel.ParagraphTitle:
                    ProcessSectionHeaderChunk(chunkElement);
                    break;
                case DetectionLabel.Caption:
                case DetectionLabel.TableCaption:
                case DetectionLabel.FigureCaption:
                    ProcessCaptionChunk(chunkElement);
                    break;
                case DetectionLabel.Footnote:
                    ProcessFootnoteChunk(chunkElement);
                    break;
                case DetectionLabel.Formula:
                    ProcessFormulaChunk(chunkElement);
                    break;
                case DetectionLabel.ListItem:
                    ProcessListItemChunk(chunkElement);
                    break;
                case DetectionLabel.PageFooter:
                    if (!_ignoreMarginalia)
                    {
                        ProcessPageFooterChunk(chunkElement);
                    }
                    break;
                case DetectionLabel.PageHeader:
                    if (!_ignoreMarginalia)
                    {
                        ProcessPageHeaderChunk(chunkElement);
                    }
                    break;
                case DetectionLabel.Number:
                case DetectionLabel.PageNumber:
                case DetectionLabel.Text:
                    ProcessTextChunk(chunkElement);
                    break;
                case DetectionLabel.Title:
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
            WriteText(chunkElement, 1);
            _writer.AppendLine();
        }

        private void ProcessTextChunk(ChunkElement chunkElement)
        {
            WriteText(chunkElement);
            _writer.AppendLine();
        }

        private void ProcessPageHeaderChunk(ChunkElement chunkElement)
        {
            WriteText(chunkElement, 3);
            _writer.AppendLine();
        }

        private void ProcessSectionHeaderChunk(ChunkElement chunkElement)
        {
            WriteText(chunkElement, 2);
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
            if (_outputFigureAsImage)
            {
                var imageName = $"img_{Guid.NewGuid().ToString()}.png";
                string tempImagePath = Path.Combine(_workFolder, imageName);
                ChunkUtils.ClipChunkRectImage(_pageImagePath, tempImagePath, chunkElement.ChunkObject, false);

                if (_embedBase64ImageData)
                {
                    var imageData = ChunkUtils.EncodeBase64ImageData(tempImagePath);
                    _writer.WritePictureWithBase64(imageData);
                }
                else
                {
                    var imageUrl = $"{WorkDirectoryOptions.RequestPath}/{_jobFolderName}/{imageName}";
                    _writer.WritePicture(imageUrl);
                }

                return;
            }

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
                WriteText(textElements, chunkElement.ChunkObject, autoLinkBreak: true);
            }
        }

        private void WriteLine(IEnumerable<ContentElement> contents, ObjectDetectionResult chunkObject, bool autoOCR, int? headingLevel = null, bool outputAsHtml = false)
        {
            var textParagraphs = LineBreakProcessor.ProcessLineBreaks(contents, chunkObject, autoOCR, _workFolder, _pageImagePath, _removeBulletChar, outputAsHtml);
            if (textParagraphs == null || textParagraphs.Count == 0)
            {
                return;
            }
            foreach (var paragraph in textParagraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph.Text))
                {
                    _writer.AppendLine();
                    continue;
                }

                if (paragraph.IsListItem)
                {
                    chunkObject.Label = DetectionLabel.ListItem;
                    _writer.WriteListItemTag(listTag: paragraph.ListTag, isOrderList: paragraph.IsOrderedList);
                }
                if (headingLevel != null)
                {
                    _writer.WriteTitleTag(headingLevel.Value);
                }
                _writer.AppendText(paragraph.Text);
            }
        }

        private void WriteText(IEnumerable<ContentElement> contents, ObjectDetectionResult chunkObject, bool autoLinkBreak = false, bool autoOCR = false, int? headingLevel = null)
        {
            if (autoLinkBreak)
            {
                WriteLine(contents, chunkObject, autoOCR, headingLevel, _useEmbeddedHtml);
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
                    bool isListParagraphBegin = TextElement.IsListParagraphBegin(text, out var ordered, out var listTag) || curr.IsWingdingFont();
                    if (prev != null)
                    {
                        if (Math.Round(prev.GetBaselineY()) == Math.Round(curr.GetBaselineY()))
                        {
                            isListParagraphBegin = false;
                        }
                    }

                    if (_useEmbeddedHtml && content.PageElement.TryBuildHTMLPiece(out string html))
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

        private void WriteText(ChunkElement chunkElement, int? headingLevel = null)
        {
            var contents = chunkElement.ContentElements;
            bool outputAsImage = false;
            if (contents == null || contents.Count() == 0)
            {
                outputAsImage = true;
            }
            else if (contents.Count() == 1)
            {
                outputAsImage = contents.ElementAt(0).PageElement is ImageElement;
            }

            if (outputAsImage)
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
                    if (_embedBase64ImageData)
                    {
                        var imageData = ChunkUtils.EncodeBase64ImageData(tempImagePath);
                        _writer.WritePictureWithBase64(imageData);
                    }
                    else
                    {
                        var imagePath = $"{WorkDirectoryOptions.RequestPath}/{_jobFolderName}/{imageName}";
                        _writer.WritePicture(imagePath);
                    }
                }

                return;
            }

            WriteText(contents, chunkElement.ChunkObject, true, _enableOCR, headingLevel);
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
            text = EscapeMarkdown(text);
            _sb.Append(text);
        }

        public void AppendLine()
        {
            _sb.Append("\n");
        }

        public void WriteTitleTag(int headingLevel = 1)
        {
            headingLevel = Math.Clamp(headingLevel, 1, 6);
            _sb.Append($"{new string('#', headingLevel)} ");
        }

        public void WriteListItemTag(string listTag = null, bool isOrderList = false, int level = 1)
        {
            if (level > 1)
            {
                _sb.Append($"{new string(' ', (level - 1) * 2)}");
            }

            if (isOrderList)
            {
                if (!string.IsNullOrEmpty(listTag))
                {
                    _sb.Append($"{listTag} ");
                }
                else
                {
                    _sb.Append($"{level}. ");
                }
            }
            else
            {
                _sb.Append("- ");
            }
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

        public void WritePictureWithBase64(string base64ImageData, string altText = "")
        {
            _sb.AppendLine($"![{altText}]({base64ImageData})");
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

        public static string EscapeMarkdown(string text)
        {
            var charsToEscape = new[] { '\\', '`', '*', '_', '~', '#', '+', '-', '!' };
            foreach (var c in charsToEscape)
            {
                text = text.Replace(c.ToString(), "\\" + c);
            }
            return text;
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

    public class ChunkElementProcessorParameter
    {
        public bool UseEmbeddedHtml { get; set; } = false;
        public bool IgnoreMarginalia { get; set; } = false;
        public bool OutputFigureAsImage { get; set; } = false;
        public bool EnableOCR { get; set; } = false;
        public bool RemoveBulletChar { get; set; } = true;
        public bool EmbedBase64ImageData { get; set; } = false;
    }
}
