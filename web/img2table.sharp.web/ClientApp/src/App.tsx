import React from 'react';
import Sidebar from './components/Sidebar';
import Toolbar from './components/Toolbar';
import PreviewPane from './components/PreviewPane';
import ExtractionPane from './components/ExtractionPane';

const App = () => {
  return (
    <div className="flex h-screen font-sans">
      <div className="w-64 bg-gray-100 p-4 border-r overflow-auto">
        <Sidebar />
      </div>
      <div className="flex-1 flex flex-col">
        <div className="p-2 border-b">
          <Toolbar />
        </div>
        <div className="flex flex-1 overflow-hidden">
          <div className="w-1/2 p-4 overflow-auto border-r">
            <PreviewPane />
          </div>
          <div className="w-1/2 p-4 overflow-auto">
            <ExtractionPane />
          </div>
        </div>
      </div>
    </div>
  );
};

export default App;