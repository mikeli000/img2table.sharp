import { Card, CardContent } from './ui/card';
import React, { useState, useRef, useEffect } from "react";

const baseUrl = import.meta.env.VITE_API_BASE_URL;

const PreviewPane = ({ documentChunks, highlight }) => {
  const [imageSizes, setImageSizes] = useState({});
  const pageRefs = useRef({});


  const handleImageLoad = (e, pageNumber) => {
    const img = e.target;
    setImageSizes((prev) => ({
      ...prev,
      [pageNumber]: {
        naturalWidth: img.naturalWidth,
        naturalHeight: img.naturalHeight,
        clientWidth: img.clientWidth,
        clientHeight: img.clientHeight,
      },
    }));
  };

  useEffect(() => {
    if (highlight?.pageNumber && pageRefs.current[highlight.pageNumber]) {
      pageRefs.current[highlight.pageNumber].scrollIntoView({
        behavior: "smooth",
        block: "center",
      });

      console.log("Highlighting page:", highlight.pageNumber);
    }
  }, [highlight]);

  return (
    <Card className="h-full overflow-auto">
      <CardContent className="space-y-4 p-4 relative">
        {documentChunks?.pagedChunks?.map((chunk, index) => {
          const isHighlighted = highlight?.pageNumber === chunk.pageNumber;
          const imageSize = imageSizes[chunk.pageNumber];

          let boxStyle = {};

          if (isHighlighted && imageSize) {
            const scaleX = imageSize.clientWidth / imageSize.naturalWidth;
            const scaleY = imageSize.clientHeight / imageSize.naturalHeight;

            const [x0, y0, x1, y1] = highlight.bbox;
            boxStyle = {
              position: "absolute",
              left: x0 * scaleX + "px",
              top: y0 * scaleY + "px",
              width: (x1 - x0) * scaleX + "px",
              height: (y1 - y0) * scaleY + "px",
              border: "1px dashed red",
              backgroundColor: "rgba(255, 0, 0, 0.1)",
              pointerEvents: "none",
              boxSizing: "border-box",
              zIndex: 10,
            };
          }

          return (
            <div
              key={index}
              ref={el => pageRefs.current[chunk.pageNumber] = el}
              className="border rounded shadow relative"
              style={{ width: "100%" }}
            >
              <div className="mb-4">
                <div className="bg-gray-100 px-2 py-1 text-sm text-gray-600">
                  Page {chunk.pageNumber}
                </div>

                <div className="relative w-full">
                  <img
                    src={`${baseUrl}${chunk.previewImagePath}`}
                    className="w-full object-contain"
                    onLoad={(e) => handleImageLoad(e, chunk.pageNumber)}
                  />
                  {isHighlighted && imageSize && (
                    <div style={boxStyle}></div>
                  )}
                </div>
              </div>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
};

export default PreviewPane;
