import React from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import ReactMarkdown from 'react-markdown';
import rehypeRaw from 'rehype-raw';

const baseUrl = import.meta.env.VITE_API_BASE_URL;

const ExtractionPane = ({ documentChunks, handleChunkClick }) => {
  // 下载JSON文件的函数
  const downloadJSON = () => {
    const dataStr = JSON.stringify(documentChunks, null, 2);
    const dataBlob = new Blob([dataStr], { type: 'application/json' });
    const url = URL.createObjectURL(dataBlob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${documentChunks?.documentName || 'document'}.json`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  return (
    <Card className="w-full">
      <CardContent className="p-4 flex flex-col">
        <Tabs defaultValue="markdown" className="flex flex-col flex-1">
          <TabsList className="flex justify-between items-center space-x-2 bg-gray-100 p-1 rounded-md">
            <div className="flex space-x-2">
              <TabsTrigger
                value="markdown"
                className="data-[state=active]:bg-white data-[state=active]:shadow-sm data-[state=active]:border data-[state=active]:border-gray-300 px-4 py-1.5 text-sm rounded-md transition"
              >
                Markdown
              </TabsTrigger>
              <TabsTrigger
                value="json"
                className="data-[state=active]:bg-white data-[state=active]:shadow-sm data-[state=active]:border data-[state=active]:border-gray-300 px-4 py-1.5 text-sm rounded-md transition"
              >
                JSON
              </TabsTrigger>
            </div>
          </TabsList>

          <TabsContent value="markdown" className="mt-4 overflow-auto flex-1">
            <div className="flex justify-between items-center mb-4">
              <h3 className="text-lg font-semibold">Markdown Content</h3>
              {/* Markdown 下载按钮 */}
              {documentChunks?.documentName && (
                <a
                  href={`${baseUrl}/job/${documentChunks.jobId}/${documentChunks.documentName}.md`}
                  download={`${documentChunks.documentName}.md`}
                  className="inline-flex items-center gap-1 bg-blue-600 hover:bg-blue-700 text-white px-3 py-1.5 rounded text-sm font-medium shadow transition"
                >
                  📄 Download MD
                </a>
              )}
            </div>
            {documentChunks?.markdown ? (
              <div className="prose text-sm max-w-none space-y-4">
                {documentChunks.pagedChunks.map((page) =>
                  page.chunks.map((chunk, index) => (
                    <div
                      key={`p${page.pageNumber}-${index}`}
                      className="relative border rounded p-3 mb-4 bg-white shadow-sm transition-all duration-200 hover:shadow-md hover:bg-gray-50 hover:border-gray-400 cursor-pointer"
                      onClick={() => handleChunkClick(page.pageNumber, chunk.chunkObject.bbox)}
                    >
                      <div className="absolute -top-2 left-2 bg-white px-1 text-xs text-gray-500">
                        {chunk.chunkObject?.label}
                      </div>

                      <div className="prose text-sm max-w-none">
                        <ReactMarkdown rehypePlugins={[rehypeRaw]}>
                          {chunk.markdownText}
                        </ReactMarkdown>
                      </div>
                    </div>
                  ))
                )}
              </div>
            ) : (
              <div className="text-gray-500 italic">No markdown content</div>
            )}
          </TabsContent>

          <TabsContent value="json" className="mt-4 overflow-auto flex-1">
            <div className="flex justify-between items-center mb-4">
              <h3 className="text-lg font-semibold">JSON Content</h3>
              {/* JSON 下载按钮 */}
              {documentChunks && (
                <button
                  onClick={downloadJSON}
                  className="inline-flex items-center gap-1 bg-green-600 hover:bg-green-700 text-white px-3 py-1.5 rounded text-sm font-medium shadow transition"
                >
                  📁 Download JSON
                </button>
              )}
            </div>
            {documentChunks ? (
              <pre className="whitespace-pre-wrap text-sm text-gray-800">
                {JSON.stringify(documentChunks, null, 2)}
              </pre>
            ) : (
              <div className="text-gray-500 italic">No JSON content</div>
            )}
          </TabsContent>
        </Tabs>
      </CardContent>
    </Card>
  );
};

export default ExtractionPane;