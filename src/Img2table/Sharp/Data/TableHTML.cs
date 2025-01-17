using HtmlAgilityPack;
using Img2table.Sharp.Data;
using System;
using System.Collections.Generic;
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
            }";

        public static void Generate(PagedTableDTO pageTableDto, string outputFile)
        {
            var htmlDoc = new HtmlDocument();
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
                        cellNode.InnerHtml = cell.Content;
                        rowNode.AppendChild(cellNode);
                    }
                    tableNode.AppendChild(rowNode);
                }

                htmlDoc.DocumentNode.AppendChild(tableNode);
            }

            StreamWriter sw = new StreamWriter(outputFile);
            htmlDoc.Save(sw);
        }
    }
}
