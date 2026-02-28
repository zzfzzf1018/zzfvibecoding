# DICOM DVH Test Tool

A web-based application for testing plan quality metrics APIs. This tool facilitates the parsing of DICOM RT files (CT, Structure Set, RT Dose), calling Dose Volume Histogram (DVH) statistics API, and generate plan quality metrics evaluation request.

## Prerequisites

- [Node.js](https://nodejs.org/) (v14+ recommended) installed on your machine.

## Installation

1. Navigate to the project directory:
2. Install the required Node.js dependencies:
   ```bash
   npm install express multer http-proxy-middleware cors
   ```

## Configuration

The application uses a configuration file `config.json` to manage environment-specific settings.

**`config.json`**:

```json
{
    "backendUrl": "http://localhost:80"
}
```

- **backendUrl**: The URL of the upstream API server that handles heavy calculation requests (e.g., Plan Quality Metrics API). The Node.js server acts as a proxy, forwarding requests from `/api` to this URL to avoid CORS issues.

## Running the Application

1. Start the local server:
   ```bash
   node server.js
   ```

2. Open your web browser and navigate to:
   ```
   http://localhost:3000
   ```

## Usage Guide

Follow the below steps:

1. **Browse Local Folder**:
   - Click **Browse Local Folder** to choose a DICOM folder which must include DICOM CT images, RT Struct, RT Dose.

2. **Scan Files**:
   - Click **"Scan Files"** to parsing the DICOM files.

3. **Run Analysis**:
   - Enter the **Prescribed Dose (Gy)**.
   - Click **"Run Analysis"**. This will send the data to the configured backend or process locally depending on implementation.
   - View the detailed metrics in the table.
   - Use the **Filter** input to search for specific ROIs (e.g., "PTV", "Lung").

4. **Evaluate Criteria**:
   - Used to test the Criteria Evaluation API (Just mock the request and dump the response)

5. **Save Request**:
   - Save the Evaluate Criteria request to a file that for postman

## Bundle into an EXE

```shell
# step1
npm install
# step2
npm run build
```

The output is in the folder **dist**

```shell
./dist/dicom-dvh-tool.exe
./dist/config.json
```
