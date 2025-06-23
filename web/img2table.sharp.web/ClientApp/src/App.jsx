import React, { useState } from 'react';
import Sidebar from './components/Sidebar';
import Toolbar from './components/Toolbar';
import PreviewPane from './components/PreviewPane';
import ExtractionPane from './components/ExtractionPane';

const App = () => {
  const [file, setFile] = useState(null);
  const [documentChunks, setDocumentChunks] = useState([]);
  const [useHtml, setUseHtml] = useState(false);

  return (
    <div className="flex h-screen font-sans bg-gray-50">
      <div className="w-72 bg-white p-4 border-r overflow-auto">
        <Sidebar setDocumentChunks={setDocumentChunks} useHtml={useHtml} />
      </div>
      <div className="flex-1 flex flex-col">
        <div className="p-2 border-b bg-white">
          <Toolbar useHtml={useHtml} setUseHtml={setUseHtml} />
        </div>
        <div className="flex flex-1 overflow-hidden">
          <div className="w-1/2 p-4 overflow-auto border-r bg-white">
            <PreviewPane file={file} documentChunks={documentChunks} />
          </div>
          <div className="w-1/2 p-4 overflow-auto bg-white">
            <ExtractionPane documentChunks={documentChunks} />
          </div>
        </div>
      </div>
    </div>
  );
};

export default App;
