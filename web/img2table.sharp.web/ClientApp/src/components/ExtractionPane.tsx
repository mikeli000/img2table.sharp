import React from 'react';

const ExtractionPane = () => {
  return (
    <div className="text-sm text-gray-800 space-y-4">
      <div>
        <h3 className="font-bold mb-1">Titles</h3>
        <ul className="list-disc list-inside">
          <li>Title A</li>
          <li>Title B</li>
        </ul>
      </div>

      <div>
        <h3 className="font-bold mb-1">List Items</h3>
        <ul className="list-decimal list-inside">
          <li>Item 1</li>
          <li>Item 2</li>
        </ul>
      </div>

      <div>
        <h3 className="font-bold mb-1">Table</h3>
        <table className="w-full border text-left">
          <thead>
            <tr><th className="border px-2 py-1">Column 1</th><th className="border px-2 py-1">Column 2</th></tr>
          </thead>
          <tbody>
            <tr><td className="border px-2 py-1">Row 1 Col 1</td><td className="border px-2 py-1">Row 1 Col 2</td></tr>
            <tr><td className="border px-2 py-1">Row 2 Col 1</td><td className="border px-2 py-1">Row 2 Col 2</td></tr>
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default ExtractionPane;