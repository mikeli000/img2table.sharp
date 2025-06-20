import React from 'react';
import { Tabs, TabsList, TabsTrigger, TabsContent } from './ui/tabs';
import { Card, CardContent } from './ui/card';

const ExtractionPane = ({ result }) => {
  return (
    <Card className="h-full">
      <CardContent className="p-4 h-full flex flex-col">
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
            <pre className="whitespace-pre-wrap text-sm text-gray-800">{result.markdown || '暂无内容'}</pre>
          </TabsContent>
          <TabsContent value="json" className="mt-4 overflow-auto flex-1">
            <pre className="whitespace-pre-wrap text-sm text-gray-800">
              {JSON.stringify(result.json, null, 2)}
            </pre>
          </TabsContent>
        </Tabs>
      </CardContent>
    </Card>
  );
};

export default ExtractionPane;
