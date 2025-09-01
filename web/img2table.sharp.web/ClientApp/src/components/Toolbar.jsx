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
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";

const Toolbar = ({ useHtml, setUseHtml, ignoreMarginalia, setIgnoreMarginalia, docType, setDocType, autoOcr, setAutoOcr }) => {
  const handleDocTypeChange = (value) => {
    const unsupportedTypes = ['spreadsheet', 'form', 'plain'];

    if (unsupportedTypes.includes(value)) {
      alert('This document type is not supported yet. Please select another option.');
      setDocType('academic');
      return;
    }

    setDocType(value);
  };

  return (
    <div className="flex items-center gap-6 text-sm text-gray-700 px-4 py-2 border-b border-gray-200 bg-white shadow-sm">
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <div className="flex items-center gap-2">
              <span className="font-medium">Document Type:</span>
              <Select value={docType} onValueChange={handleDocTypeChange} disabled>
                <SelectTrigger className="w-[220px] bg-gray-100 text-gray-400 cursor-not-allowed">
                  <SelectValue placeholder="Select document type..." />
                </SelectTrigger>
                <SelectContent className="z-50">
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
          </TooltipTrigger>
          <TooltipContent
            side="right"
            className="z-40 overflow-hidden rounded-lg bg-[rgba(0,0,0,0.85)] px-4 py-2 text-sm text-white shadow-lg max-w-sm leading-relaxed"
          >
            <p>
              Select the appropriate layout model based on the type of PDF document, such as academic papers or slides.
              <br />
              <em>Currently, only <strong>Academic Paper</strong> and <strong>Slide-style</strong> are supported. Other types are not yet available.</em>
            </p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>

      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <div className="flex items-center space-x-2">
              <Checkbox
                id="parse-text-style"
                checked={useHtml}
                disabled
                onCheckedChange={val => setUseHtml(!!val)}
              />
              <label htmlFor="parse-text-style" className="text-sm">
                Parse Text Styles
              </label>
            </div>
          </TooltipTrigger>
          <TooltipContent
            side="bottom"
            className="z-50 overflow-hidden rounded-lg bg-[rgba(0,0,0,0.85)] px-4 py-2 text-sm text-white shadow-lg max-w-sm leading-relaxed"
          >
            <p>
              Parse rich text styles such as <strong>font color</strong>, <strong>font size</strong>, and other inline formatting and embedded HTML in Markdown.<br />
              <em>Currently only <strong>font color</strong> is supported.</em>
            </p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>


      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <div className="flex items-center space-x-2">
              <Checkbox
                id="filter-page-noise"
                checked={ignoreMarginalia}
                onCheckedChange={(val) => setIgnoreMarginalia(val === true)}
              />
              <label htmlFor="filter-page-noise" className="text-sm">
                Ignore Noisy Blocks
              </label>
            </div>
          </TooltipTrigger>
          <TooltipContent
            side="bottom"
            className="z-50 overflow-hidden rounded-lg bg-[rgba(0,0,0,0.85)] px-4 py-2 text-sm text-white shadow-lg max-w-sm leading-relaxed">
            <p>
              Ignore unimportant content such as headers, footers, page numbers, or isolated non-informative text blocks.
            </p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>

      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <div className="flex items-center space-x-2">
              <Checkbox
                id="auto-ocr"
                checked={autoOcr}
                onCheckedChange={(val) => setAutoOcr(val === true)}
              />
              <label htmlFor="auto-ocr" className="text-sm">Smart OCR</label>
            </div>
          </TooltipTrigger>
          <TooltipContent side="right" className="bg-gray-800 text-white text-xs p-2 rounded shadow-md max-w-xs">
            Automatically apply OCR to regions classified as text-like. Others will be output as images.
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>

      <div className="ml-auto">
        <a
          href="https://seismic.atlassian.net/wiki/spaces/CSTR/pages/4959305795/Dive+into+PDF+Document+Extraction"
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-1 text-white bg-blue-600 hover:bg-blue-700 px-3 py-1.5 rounded text-sm font-medium shadow"
        >
          📘 Docs
        </a>
      </div>
    </div>
  );
};

export default Toolbar;
