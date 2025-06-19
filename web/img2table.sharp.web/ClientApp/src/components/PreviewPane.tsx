import React from 'react';

const PreviewPane = () => {
  return (
    <div className="flex flex-col items-center">
      <img src="/sample-doc-page.png" alt="Preview" className="w-full max-w-[800px] border" />
      <div className="mt-2">
        <button className="px-3 py-1 border">Prev</button>
        <span className="mx-2">Page 1 of 5</span>
        <button className="px-3 py-1 border">Next</button>
      </div>
    </div>
  );
};

export default PreviewPane;