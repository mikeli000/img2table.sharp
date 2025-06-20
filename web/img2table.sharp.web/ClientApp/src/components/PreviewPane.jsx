import React from 'react';
import { Card, CardContent } from './ui/card';

const PreviewPane = ({ file }) => {
  if (!file) return (
    <Card className="h-full">
      <CardContent className="text-gray-500 p-4">请上传文件以预览</CardContent>
    </Card>
  );

  const fileURL = URL.createObjectURL(file);
  const isPDF = file.type === 'application/pdf';

  return (
    <Card className="h-full">
      <CardContent className="h-full p-0">
        {isPDF ? (
          <iframe src={fileURL} className="w-full h-full" title="PDF预览" />
        ) : (
          <img src={fileURL} alt="预览图像" className="w-full h-full object-contain" />
        )}
      </CardContent>
    </Card>
  );
};

export default PreviewPane;
