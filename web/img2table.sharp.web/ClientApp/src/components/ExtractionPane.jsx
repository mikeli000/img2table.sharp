import React from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import ReactMarkdown from 'react-markdown';
import rehypeRaw from 'rehype-raw';

const ExtractionPane = ({ documentChunks, handleChunkClick }) => {
    return (
    <Card className="w-full">
      <CardContent className="p-4 flex flex-col">
        <Tabs defaultValue="markdown" className="flex flex-col flex-1">
          <TabsList className="flex space-x-2 bg-gray-100 p-1 rounded-md">
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
          </TabsList>

          <TabsContent value="markdown" className="mt-4 overflow-auto flex-1">
            {documentChunks.markdown ? (
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
                        <ReactMarkdown>
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
