import React, { useRef, useState } from 'react';
const baseUrl = import.meta.env.VITE_API_BASE_URL;

const Sidebar = ({ setDocumentChunks, useHtml, ignoreMarginalia, docType, uploading, setUploading }) => {
  const fileInputRef = useRef(null);
  const [uploadedFiles, setUploadedFiles] = useState([]);

  const handleDrop = (e) => {
    e.preventDefault();
    const file = e.dataTransfer.files[0];
    if (file && file.type === 'application/pdf') {
      uploadFile(file);
    } else {
      alert('Only PDF files are allowed.');
    }
  };

  const handleUploadClick = () => {
    fileInputRef.current.click();
  };

  const handleFileChange = (e) => {

    console.log('useHtml:', useHtml);

    const file = e.target.files[0];
    if (file && file.type === 'application/pdf') {
      uploadFile(file);
    } else {
      alert('Only PDF files are allowed.');
    }
  };

  const uploadFile = async (file) => {
    setUploading(true);
    const formData = new FormData();
    formData.append('uploadFile', file);
    formData.append("useEmbeddedHtml", useHtml ? "true" : "false");
    formData.append("ignoreMarginalia", ignoreMarginalia ? "true" : "false");
    formData.append("docType", docType || "slide");

    try {
      const response = await fetch(`${baseUrl}/api/extract`, {
        method: 'POST',
        body: formData,
      });
      const result = await response.json();
      setDocumentChunks(result || []);

      console.log('pagedChunks:', result);

      if (response.ok) {
        setUploadedFiles((prev) => [...prev, file.name]);
      } else {
        alert('Upload failed.');
      }
    } catch (error) {
      console.error('Upload error:', error);
      alert('Upload error. Check console for details.');
    } finally {
      setUploading(false);
    }
  };

  const handleShowcaseClick = async (e, fileName) => {
    e.preventDefault();
    try {
      const res = await fetch(`${baseUrl}/showcase/${fileName}`);
      const blob = await res.blob();
      const file = new File([blob], fileName, { type: 'application/pdf' });
      await uploadFile(file);
    } catch (err) {
      alert('Failed to load showcase file.');
      console.error(err);
    }
  };

  return (
    <div>
      {/* 上传区域 */}
      <div
        className="border-dashed border-2 border-gray-300 rounded-md p-4 text-center hover:bg-gray-50 cursor-pointer transition"
        onClick={handleUploadClick}
        onDragOver={(e) => e.preventDefault()}
        onDrop={handleDrop}
      >
        <p className="text-sm text-gray-600 mb-2">Drag and drop a PDF here, or click to upload</p>
        <input
          type="file"
          accept="application/pdf"
          ref={fileInputRef}
          className="hidden"
          onChange={handleFileChange}
        />
        {uploading && <p className="text-blue-500 mt-2">Uploading...</p>}

        <div className="mt-4 text-left">
          <h2 className="font-semibold text-gray-800 mb-2">Uploaded Files:</h2>
          <ul className="text-sm text-gray-700 space-y-1">
            {uploadedFiles.map((name, idx) => (
              <li key={idx} className="truncate">{name}</li>
            ))}
          </ul>
        </div>
      </div>

      <div className="my-6 border-t border-gray-200"></div>

      <div className="text-left bg-gray-50 rounded-md p-4 shadow-sm">
        <h2 className="font-semibold text-gray-800 mb-3 flex items-center">
          <svg className="w-5 h-5 mr-2 text-blue-500" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V7a2 2 0 00-2-2h-7l-2-2H5a2 2 0 00-2 2z" />
          </svg>
          Showcase Files
        </h2>
        <ul className="text-sm text-gray-700 space-y-2">
          <li>
            <a
              href={`${baseUrl}/showcase/Aura Copilot at Shift 2024.pdf`}
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center px-3 py-2 rounded hover:bg-blue-50 transition group"
              onClick={e => handleShowcaseClick(e, 'Aura Copilot at Shift 2024.pdf')}
            >
              <span className="truncate text-blue-700 group-hover:underline">Aura Copilot at Shift 2024.pdf</span>
            </a>
          </li>
          <li>
            <a
              href={`${baseUrl}/showcase/OutSystems Leaves Highspot.pdf`}
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center px-3 py-2 rounded hover:bg-blue-50 transition group"
              onClick={e => handleShowcaseClick(e, 'OutSystems Leaves Highspot.pdf')}
            >
              <span className="truncate text-blue-700 group-hover:underline">OutSystems Leaves Highspot.pdf</span>
            </a>
          </li>
          <li>
            <a
              href={`${baseUrl}/showcase/Seismic vs. Highspot Deck_Premier Ed.pdf`}
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center px-3 py-2 rounded hover:bg-blue-50 transition group"
              onClick={e => handleShowcaseClick(e, 'Seismic vs. Highspot Deck_Premier Ed.pdf')}
            >
              <span className="truncate text-blue-700 group-hover:underline">Seismic vs. Highspot Deck_Premier Ed.pdf</span>
            </a>
          </li>
          <li>
            <a
              href={`${baseUrl}/showcase/CMS 3.0 Introduction.pdf`}
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center px-3 py-2 rounded hover:bg-blue-50 transition group"
              onClick={e => handleShowcaseClick(e, 'CMS 3.0 Introduction.pdf')}
            >
              <span className="truncate text-blue-700 group-hover:underline">CMS 3.0 Introduction.pdf</span>
            </a>
          </li>
          <li>
            <a
              href={`${baseUrl}/showcase/Illumina COVIDSeq Test.pdf`}
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center px-3 py-2 rounded hover:bg-blue-50 transition group"
              onClick={e => handleShowcaseClick(e, 'Illumina COVIDSeq Test.pdf')}
            >
              <span className="truncate text-blue-700 group-hover:underline">Illumina COVIDSeq Test.pdf</span>
            </a>
          </li>
          <li>
            <a
              href={`${baseUrl}/showcase/PDFUA-Ref-2-02_Invoice.pdf`}
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center px-3 py-2 rounded hover:bg-blue-50 transition group"
              onClick={e => handleShowcaseClick(e, 'PDFUA-Ref-2-02_Invoice.pdf')}
            >
              <span className="truncate text-blue-700 group-hover:underline">PDFUA-Ref-2-02_Invoice.pdf</span>
            </a>
          </li>

        </ul>
      </div>
    </div>
  );
};

export default Sidebar;
