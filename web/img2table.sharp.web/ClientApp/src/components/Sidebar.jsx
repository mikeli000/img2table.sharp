import React, { useRef, useState } from 'react';
const baseUrl = import.meta.env.VITE_API_BASE_URL;

const Sidebar = ({ setDocumentChunks, useHtml, ignoreMarginalia }) => {
  const fileInputRef = useRef(null);
  const [uploadedFiles, setUploadedFiles] = useState([]);
  const [uploading, setUploading] = useState(false);

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

  return (
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
  );
};

export default Sidebar;
