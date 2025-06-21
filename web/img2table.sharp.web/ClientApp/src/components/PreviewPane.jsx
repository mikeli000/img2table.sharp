import React from 'react';
import { Card, CardContent } from './ui/card';
const baseUrl = import.meta.env.VITE_API_BASE_URL;

const PreviewPane = ({ file, documentChunks }) => {
    if (!documentChunks) {
    return (
      <Card className="h-full">
        <CardContent className="text-gray-500 p-4">Please upload a file to preview</CardContent>
      </Card>
    );
  }

  return (
    <Card className="h-full overflow-auto">
      <CardContent className="space-y-4 p-4">
        {documentChunks?.pagedChunks?.map((chunk, index) => (
          <div key={index} className="border rounded shadow">
            <div className="bg-gray-100 px-2 py-1 text-sm text-gray-600">
              Page {chunk.pageNumber}
            </div>
            <img
              src={`${baseUrl}${chunk.previewImagePath}`}
              alt={`Preview page ${chunk.pageNumber}`}
              className="w-full max-w-full object-contain"
            />
          </div>
        ))}
      </CardContent>
    </Card>
  );
};

export default PreviewPane;
