using HtmlAgilityPack;
using Img2table.Sharp.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.Img2table.Sharp.Data
{
    public class TableHTML
    {
        private const string DefaultStyle = @"
            table, th, td {
              border: 1px solid black;
              border-collapse: collapse;
            }";

        public static void Generate(PagedTableDTO pageTableDto, string outputFile)
        {
            var htmlDoc = new HtmlDocument();
            GenerateHtml(pageTableDto, htmlDoc);
            StreamWriter sw = new StreamWriter(outputFile);
            htmlDoc.Save(sw);
        }

        public static void Generate(PagedTableDTO pageTableDto, TextWriter textWriter)
        {
            var htmlDoc = new HtmlDocument();
            GenerateHtml(pageTableDto, htmlDoc);
            htmlDoc.Save(textWriter);
        }

        public static void Generate(PagedTableDTO pageTableDto, out string html)
        {
            var htmlDoc = new HtmlDocument();
            GenerateHtml(pageTableDto, htmlDoc);
            using (var sw = new StringWriter())
            {
                htmlDoc.Save(sw);
                html = sw.ToString();
            }
        }

        public static void Generate(TableDTO tableDTO, out string tableHtml, bool firstRowAsTH = false)
        {
            var htmlDoc = new HtmlDocument();
            GenerateHtmlTable(tableDTO, htmlDoc, firstRowAsTH);
            using (var sw = new StringWriter())
            {
                htmlDoc.Save(sw);
                tableHtml = sw.ToString();
            }
        }

        private static void GenerateHtmlTable(TableDTO tableDto, HtmlDocument htmlDoc, bool firstRowAsTH = false)
        {
            var tableNode = htmlDoc.CreateElement("table");
            if (!string.IsNullOrEmpty(tableDto.Title))
            {
                tableNode.SetAttributeValue("borderless", tableDto.Borderless.ToString());
                tableNode.SetAttributeValue("title", tableDto.Title);

                var captionNode = htmlDoc.CreateElement("caption");
                captionNode.InnerHtml = tableDto.Title;
                tableNode.AppendChild(captionNode);
            }

            var firstRow = tableDto.Items.FirstOrDefault();
            if (firstRow != null)
            {
                foreach (var cell in firstRow.Items)
                {
                    if (cell.RowSpan > 1)
                    {
                        firstRowAsTH = false;
                        break;
                    }
                }
            }

            for (int i = 0; i < tableDto.Items.Count; i++)
            {
                var row = tableDto.Items[i];
                if (firstRowAsTH && i == 0)
                {
                    var theadNode = htmlDoc.CreateElement("thead");
                    var rowNode = htmlDoc.CreateElement("tr");
                    for (int j = 0; j < row.Items.Count; j++)
                    {
                        var cell = row.Items[j];
                        var cellNode = htmlDoc.CreateElement("th");
                        if (cell.RowSpan > 1)
                        {
                            cellNode.SetAttributeValue("rowspan", cell.RowSpan.ToString());
                        }

                        if (cell.ColSpan > 1)
                        {
                            cellNode.SetAttributeValue("colspan", cell.ColSpan.ToString());
                        }
                        cellNode.InnerHtml = ProcessNewline(cell.Content);
                        rowNode.AppendChild(cellNode);
                    }
                    theadNode.AppendChild(rowNode);
                    tableNode.AppendChild(theadNode);
                }
                else
                {
                    var rowNode = htmlDoc.CreateElement("tr");
                    for (int j = 0; j < row.Items.Count; j++)
                    {
                        var cell = row.Items[j];
                        var cellNode = htmlDoc.CreateElement("td");
                        if (cell.RowSpan > 1)
                        {
                            cellNode.SetAttributeValue("rowspan", cell.RowSpan.ToString());
                        }

                        if (cell.ColSpan > 1)
                        {
                            cellNode.SetAttributeValue("colspan", cell.ColSpan.ToString());
                        }
                        cellNode.InnerHtml = ProcessNewline(cell.Content);
                        rowNode.AppendChild(cellNode);
                    }
                    tableNode.AppendChild(rowNode);
                }
            }

            htmlDoc.DocumentNode.AppendChild(tableNode);
        }

        private static void GenerateHtml(PagedTableDTO pageTableDto, HtmlDocument htmlDoc)
        {
            var root = htmlDoc.CreateElement("html");
            htmlDoc.DocumentNode.AppendChild(root);

            HtmlNode head = root.SelectSingleNode("/html/head");
            if (head == null)
            {
                head = htmlDoc.CreateElement("head");
                root.AppendChild(head);
            }

            HtmlNode charset = htmlDoc.CreateElement("meta");
            charset.Attributes.Add("charset", Encoding.UTF8.WebName);
            head.AppendChild(charset);

            HtmlNode meta = htmlDoc.CreateElement("meta");
            meta.SetAttributeValue("pageNo", pageTableDto.PageIndex + 1 + "");
            head.AppendChild(meta);

            HtmlNode style = htmlDoc.CreateElement("style");
            style.AppendChild(htmlDoc.CreateTextNode(DefaultStyle));
            head.AppendChild(style);

            HtmlNode body = htmlDoc.CreateElement("body");
            root.AppendChild(body);

            for (int i = 0; i < pageTableDto.Tables.Count; i++)
            {
                var tableDto = pageTableDto.Tables[i];

                var tableNode = htmlDoc.CreateElement("table");
                if (!string.IsNullOrEmpty(tableDto.Title))
                {
                    tableNode.SetAttributeValue("borderless", tableDto.Borderless.ToString());
                    tableNode.SetAttributeValue("title", tableDto.Title);

                    var captionNode = htmlDoc.CreateElement("caption");
                    captionNode.InnerHtml = tableDto.Title;
                    tableNode.AppendChild(captionNode);
                }

                foreach (var row in tableDto.Items)
                {
                    var rowNode = htmlDoc.CreateElement("tr");
                    for (int j = 0; j < row.Items.Count; j++)
                    {
                        var cell = row.Items[j];
                        var cellNode = htmlDoc.CreateElement("td");
                        if (cell.ColSpan > 1)
                        {
                            cellNode.SetAttributeValue("colspan", cell.ColSpan.ToString());
                            j += cell.ColSpan - 1;
                        }
                        cellNode.InnerHtml = ProcessNewline(cell.Content);
                        rowNode.AppendChild(cellNode);
                    }
                    tableNode.AppendChild(rowNode);
                }

                htmlDoc.DocumentNode.AppendChild(tableNode);
            }
        }

        private static string ProcessNewline(string text)
        {
            // text = text.Replace(Environment.NewLine, "<br />");
            return text;
        }
    }
}
