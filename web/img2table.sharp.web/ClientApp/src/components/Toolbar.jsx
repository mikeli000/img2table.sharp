'use client';

import { Select, SelectTrigger, SelectContent, SelectItem, SelectValue } from "@/components/ui/select";
import { FileText, BookOpen, LayoutTemplate, FileSpreadsheet, ScrollText } from "lucide-react";
import { useState } from "react";

const Toolbar = () => {
  const [docType, setDocType] = useState("slide");

  return (
    <div className="flex items-center gap-4 text-sm text-gray-700 px-4 py-2 border-b border-gray-200 bg-white shadow-sm">
      <span className="font-medium">Document Type:</span>
      <Select value={docType} onValueChange={setDocType}>
        <SelectTrigger className="w-[220px]">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="slide" className="flex items-center gap-2 whitespace-nowrap">
            <LayoutTemplate className="w-4 h-4" />
            Slide-style
          </SelectItem>
          <SelectItem value="academic" className="flex items-center gap-2 whitespace-nowrap">
            <BookOpen className="w-4 h-4" />
            Academic Paper
          </SelectItem>
          <SelectItem value="spreadsheet" className="flex items-center gap-2 whitespace-nowrap">
            <FileSpreadsheet className="w-4 h-4" />
            Spreadsheet-like
          </SelectItem>
          <SelectItem value="form" className="flex items-center gap-2 whitespace-nowrap">
            <FileText className="w-4 h-4" />
            Business Form
          </SelectItem>
          <SelectItem value="plain" className="flex items-center gap-2 whitespace-nowrap">
            <ScrollText className="w-4 h-4" />
            Plain Text
          </SelectItem>
        </SelectContent>
      </Select>
    </div>
  );
};

export default Toolbar;
