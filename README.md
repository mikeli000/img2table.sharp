# PDF Document Extraction
The project combines model-based recognition with program-based computation to accurately understand and extract structured content from PDF documents.

At its core, it leverages deep learning-based document layout inference models alongside multiple algorithms to accurately identify structural elements such as titles, tables, lists, headers, footers, and more. The extracted content is then intelligently segmented into coherent chunks and outputted in well-structured Markdown or HTML formats, enabling more precise and meaningful prompts for LLM when processing PDF documents.

## Table of Contents

- [Installation](#installation)
- [Usage](#usage)
- [Contributing](#contributing)
- [License](#license)
- [Contact](#contact)

## Installation

To install `img2table.sharp`, you can clone the repository and build the project using .NET.

```bash
# Clone the repository
git clone https://github.com/your-username/img2table.sharp.git

# Navigate to the project directory
cd img2table.sharp

# Restore dependencies
dotnet restore

# Build the project
dotnet build
