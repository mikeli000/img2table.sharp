'use client';

import { useState } from "react";
import {
  Select, SelectTrigger, SelectContent, SelectItem, SelectValue
} from "@/components/ui/select";
import { Checkbox } from "@/components/ui/checkbox";
import {
  LayoutTemplate, BookOpen, FileSpreadsheet,
  FileText, ScrollText
} from "lucide-react";

const Toolbar = ({ useHtml, setUseHtml, ignoreMarginalia, setIgnoreMarginalia, docType, setDocType }) => {

  return (
    <div className="flex items-center gap-6 text-sm text-gray-700 px-4 py-2 border-b border-gray-200 bg-white shadow-sm">

      {/* 文档类型选择 */}
      <div className="flex items-center gap-2">
        <span className="font-medium">Document Type:</span>
        <Select value={docType} onValueChange={setDocType}>
          <SelectTrigger className="w-[220px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="academic">
              <div className="flex items-center gap-2 whitespace-nowrap">
                <BookOpen className="w-4 h-4" />
                <span>Academic Paper</span>
              </div>
            </SelectItem>
            <SelectItem value="slide">
              <div className="flex items-center gap-2 whitespace-nowrap">
                <LayoutTemplate className="w-4 h-4" />
                <span>Slide-style</span>
              </div>
            </SelectItem>

            <SelectItem value="spreadsheet">
              <div className="flex items-center gap-2 whitespace-nowrap">
                <FileSpreadsheet className="w-4 h-4" />
                <span>Spreadsheet-like</span>
              </div>
            </SelectItem>
            <SelectItem value="form">
              <div className="flex items-center gap-2 whitespace-nowrap">
                <FileText className="w-4 h-4" />
                <span>Business Form</span>
              </div>
            </SelectItem>
            <SelectItem value="plain">
              <div className="flex items-center gap-2 whitespace-nowrap">
                <ScrollText className="w-4 h-4" />
                <span>Plain Text</span>
              </div>
            </SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* HTML checkbox */}
      <div className="flex items-center space-x-2">
        <Checkbox id="use-html" checked={useHtml} onCheckedChange={val => setUseHtml(!!val)} />
        <label htmlFor="use-html" className="text-sm leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70">
          Use HTML in Markdown
        </label>
      </div>
      <div className="flex items-center space-x-2">
        <Checkbox
          id="ignore-marginalia"
          checked={ignoreMarginalia}
          onCheckedChange={(val) => setIgnoreMarginalia(val === true)}
        />
        <label htmlFor="ignore-marginalia" className="text-sm">Ignore Marginalia</label>
      </div>
    </div>
  );
};

export default Toolbar;
