import React from 'react';

const Toolbar = () => {
  return (
    <div className="flex gap-2">
      <button className="px-4 py-1 bg-blue-500 text-white rounded">Parse</button>
      <button className="px-4 py-1 bg-green-500 text-white rounded">Extract</button>
      <button className="px-4 py-1 bg-gray-500 text-white rounded">Preview</button>
      <button className="px-4 py-1 bg-purple-500 text-white rounded">Chat</button>
    </div>
  );
};

export default Toolbar;