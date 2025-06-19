import React from 'react';

const Sidebar = () => {
  return (
    <div className="space-y-4">
      <h2 className="text-lg font-semibold">Document Files</h2>
      <button className="px-4 py-2 bg-blue-600 text-white rounded w-full">Upload File</button>
      <ul className="text-sm text-gray-700 space-y-1 pt-2">
        <li className="hover:text-blue-600 cursor-pointer">sample1.pdf</li>
        <li className="hover:text-blue-600 cursor-pointer">sample2.pdf</li>
      </ul>
    </div>
  );
};

export default Sidebar;