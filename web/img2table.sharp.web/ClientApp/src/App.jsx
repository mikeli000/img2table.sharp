import React, { useState } from 'react';
import Sidebar from './components/Sidebar';
import Toolbar from './components/Toolbar';
import PreviewPane from './components/PreviewPane';
import ExtractionPane from './components/ExtractionPane';
import { Loader } from 'lucide-react'; 

const App = () => {
  const [file, setFile] = useState(null);
  const [documentChunks, setDocumentChunks] = useState([]);
  const [useHtml, setUseHtml] = useState(false);
  const [ignoreMarginalia, setIgnoreMarginalia] = useState(true);
  const [docType, setDocType] = useState("academic");
  const [uploading, setUploading] = useState(false);
  const [highlight, setHighlight] = useState(null);
  const [autoOcr, setAutoOcr] = useState(false);

  const handleChunkClick = (pageNumber, bbox) => {
    setHighlight({ pageNumber, bbox });
  };
  
  return (
    <div className="flex h-screen font-sans bg-gray-50">
      {uploading && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center">
          <div className="bg-white px-6 py-4 rounded shadow text-gray-800 text-sm flex items-center gap-2">
            <Loader className="w-4 h-4 animate-spin" />
            Processing document, please wait...
          </div>
        </div>
      )}
      <div className="w-72 bg-white p-4 border-r overflow-auto">
        <Sidebar 
          setDocumentChunks={setDocumentChunks} 
          useHtml={useHtml} 
          ignoreMarginalia={ignoreMarginalia}
          autoOcr={autoOcr}
          docType={docType}
          setHighlight={setHighlight}
          uploading={uploading}
          setUploading={setUploading}/>
      </div>
      <div className="flex-1 flex flex-col">
        <div className="p-2 border-b bg-white">
          <Toolbar
            useHtml={useHtml}
            setUseHtml={setUseHtml}
            ignoreMarginalia={ignoreMarginalia}
            setIgnoreMarginalia={setIgnoreMarginalia}
            docType={docType}
            setDocType={setDocType}
            autoOcr={autoOcr} 
            setAutoOcr={setAutoOcr}
          />
        </div>
        <div className="flex flex-1 overflow-hidden">
          <div className="w-1/2 p-4 overflow-auto border-r bg-white">
            <PreviewPane file={file} documentChunks={documentChunks} highlight={highlight} />
          </div>
          <div className="w-1/2 p-4 overflow-auto bg-white">
            <ExtractionPane documentChunks={documentChunks} handleChunkClick={handleChunkClick} />
          </div>
        </div>
      </div>
    </div>
  );
};

export default App;
