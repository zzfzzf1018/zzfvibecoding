
// Initialize Chart instance variable
let dvhChartInstance = null;
let lastAnalysisResult = null; // Store the last statistics result for evaluation
let loadedDicomFiles = {
    ct: [],
    struct: null,
    dose: null
};

let cacheResponse = {
    stats: null
};

// --- Drag & Drop Init ---
document.addEventListener('DOMContentLoaded', function() {
    const el = document.getElementById('reportLayout');
    if (el) {
        Sortable.create(el, {
            animation: 150,
            handle: '.card-header', // Drag via header
            ghostClass: 'bg-light', // Class to apply to the dragged element
            onStart: function (evt) {
                // Optional: visual feedback
                document.body.style.cursor = 'grabbing';
            },
            onEnd: function (evt) {
                document.body.style.cursor = 'default';
            }
        });
    }

    // --- Table Column Reorder ---
    const table = document.getElementById('metricsTable');
    if (table) {
        const headers = table.querySelector('thead tr');
        if (headers) {
             Sortable.create(headers, {
                animation: 150,
                ghostClass: 'bg-light',
                onEnd: function (evt) {
                    const oldIndex = evt.oldIndex;
                    const newIndex = evt.newIndex;
                    if (oldIndex === newIndex) return;

                    // Reorder rows in tbody
                    const tbody = document.getElementById('metricsTableBody');
                    if (tbody) {
                        const rows = Array.from(tbody.querySelectorAll('tr'));
                        rows.forEach(row => {
                            // Only move if row has enough cells (ignore empty state rows if checked by length)
                            // But empty state row has colspan=7, so children.length is 1.
                            // While headers length is 7.
                            // So check if row.children.length matches headers length or is sufficient.
                            if (row.children.length > Math.max(oldIndex, newIndex)) {
                                const rowChildren = Array.from(row.children);
                                const cellToMove = rowChildren[oldIndex];
                                
                                if (oldIndex < newIndex) {
                                    const refNode = rowChildren[newIndex + 1] || null;
                                    row.insertBefore(cellToMove, refNode);
                                } else {
                                    const refNode = rowChildren[newIndex];
                                    row.insertBefore(cellToMove, refNode);
                                }
                            }
                        });
                    }
                }
            });
        }
    }
});

// --- Notification Helper ---
function showNotification(title, text, icon = 'info') {
    Swal.fire({
        title: title,
        text: text,
        icon: icon,
        confirmButtonColor: '#2563eb', // const --primary
        buttonsStyling: true,
        customClass: {
            confirmButton: 'btn btn-primary px-4'
        }
    });
}

// --- Loading Helper ---
function showLoading(text = 'Processing...') {
    const overlay = document.getElementById('loadingOverlay');
    const msg = document.getElementById('loadingText');
    if (overlay && msg) {
        msg.innerText = text;
        overlay.classList.remove('d-none');
    }
}

function hideLoading() {
    const overlay = document.getElementById('loadingOverlay');
    if (overlay) {
        overlay.classList.add('d-none');
    }
}

// --- Debounce Helper ---
function debounce(func, wait) {
    let timeout;
    return function(...args) {
        const context = this;
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(context, args), wait);
    };
}

// --- Sort Logic ---
let currentSortColumn = -1;
let currentSortDirection = 'asc';

function sortTable(columnIndex, type) {
    const tableBody = document.getElementById('metricsTableBody');
    const rows = Array.from(tableBody.querySelectorAll('tr'));
    
    // Determine sort direction
    if (currentSortColumn === columnIndex) {
        currentSortDirection = currentSortDirection === 'asc' ? 'desc' : 'asc';
    } else {
        currentSortColumn = columnIndex;
        currentSortDirection = 'asc';
    }

    // Sort Logic
    rows.sort((rowA, rowB) => {
        // Extract text content safely. The cell structure is simple <td>text</td>
        // Note: For valid first column, we must remove the badge/whitespace artifacts
        const cellA_raw = rowA.cells[columnIndex].innerText.trim();
        const cellB_raw = rowB.cells[columnIndex].innerText.trim();
        
        // Remove empty placeholders if any (e.g., if we were sorting empty rows)
        if (!cellA_raw && !cellB_raw) return 0;
        
        if (type === 'number') {
            // Remove non-numeric chars but keep decimal points or dashes
            const valA = parseFloat(cellA_raw.replace(/[^-0-9.]/g, '')) || -9999;
            const valB = parseFloat(cellB_raw.replace(/[^-0-9.]/g, '')) || -9999;
            // Handle '-' case
            if (cellA_raw === '-') return 1; 
            if (cellB_raw === '-') return -1;
            
            return currentSortDirection === 'asc' ? valA - valB : valB - valA;
        } else {
            // String sort
            return currentSortDirection === 'asc' ? 
                cellA_raw.localeCompare(cellB_raw) : 
                cellB_raw.localeCompare(cellA_raw);
        }
    });

    // Reorder DOM
    tableBody.innerHTML = '';
    rows.forEach(row => tableBody.appendChild(row));
    
    // Update Icons
    updateSortIcons(columnIndex, currentSortDirection);
}

function updateSortIcons(columnIndex, direction) {
    const headers = document.querySelectorAll('#metricsTable thead th i');
    
    // Reset all icons to default 'sort' (faded)
    headers.forEach(icon => {
        icon.className = 'fa-solid fa-sort text-muted small ms-1 opacity-25';
    });

    // Set active icon
    if (headers[columnIndex]) {
        const activeIcon = headers[columnIndex];
        activeIcon.classList.remove('text-muted', 'opacity-25', 'fa-sort');
        activeIcon.classList.add('text-primary'); // Highlight color
        if (direction === 'asc') {
            activeIcon.classList.add('fa-sort-up');
        } else {
            activeIcon.classList.add('fa-sort-down');
        }
    }
}

function handleROIFilter(e) {
    const term = e.target.value.toLowerCase();
    
    // 1. Filter Table Rows
    const tableBody = document.getElementById('metricsTableBody');
    const rows = tableBody.querySelectorAll('tr');
    
    rows.forEach(row => {
        // The first cell contains the ROI Name (and a badge)
        // Using innerText will include the invisible badge text if not careful, 
        // but simple includes check is robust enough for fuzzy match.
        const nameCell = row.cells[0]; 
        if (nameCell) {
            const text = nameCell.innerText.toLowerCase();
            if (text.includes(term)) {
                row.style.display = '';
            } else {
                row.style.display = 'none';
            }
        }
    });

    // 2. Filter DVH Chart Lines
    if (dvhChartInstance && dvhChartInstance.data && dvhChartInstance.data.datasets) {
        let changed = false;
        dvhChartInstance.data.datasets.forEach(ds => {
            const label = (ds.label || '').toLowerCase();
            // Store previous hidden state to avoid unnecessary updates
            const shouldHide = !label.includes(term);
            
            // Chart.js uses 'hidden: true' to hide, 'hidden: false/null/undefined' to show
            // Note: Chart.js logic can be tricky if legend was clicked by user. 
            // We force visibility based on search.
            if (ds.hidden !== shouldHide) {
                ds.hidden = shouldHide;
                changed = true;
            }
        });

        if (changed) {
            dvhChartInstance.update();
        }
    }
}

// UI Element Event Listeners
document.getElementById('calcBtn').addEventListener('click', handleCalculation);
document.getElementById('evaluateBtn').addEventListener('click', handleCriteriaEvaluation);
document.getElementById('saveRequestBtn').addEventListener('click', handleSaveEvalRequest);
document.getElementById('demoBtn').addEventListener('click', loadDemoData);
document.getElementById('scanBtn').addEventListener('click', handleScanFolder);
document.getElementById('roiSearchInput').addEventListener('input', debounce(handleROIFilter, 300));
// Custom Folder Selection Logic
let selectedFilesCache = []; // Store file objects here
const folderInput = document.getElementById('dicomFolder');
const browseBtn = document.getElementById('btnBrowseFolder');
const statusInput = document.getElementById('folderStatus');

browseBtn.addEventListener('click', async () => {
    // Option 2: Use File System Access API if available
    if ('showDirectoryPicker' in window) {
        try {
            const dirHandle = await window.showDirectoryPicker();
            statusInput.innerText = "Reading files..."; // Updated from .value
            selectedFilesCache = [];
            
            // Recursive function to traverse directory
            async function readDirectory(handle) {
                for await (const entry of handle.values()) {
                    if (entry.kind === 'file') {
                        const file = await entry.getFile();
                        selectedFilesCache.push(file);
                    } else if (entry.kind === 'directory') {
                        await readDirectory(entry);
                    }
                }
            }
            
            await readDirectory(dirHandle);
            statusInput.innerText = `${selectedFilesCache.length} files selected`; // Updated from .value
            
        } catch (err) {
            if (err.name !== 'AbortError') {
                console.error(err);
                showNotification("Access Error", "Error accessing folder: " + err.message, "error");
            }
        }
    } else {
        // Fallback for browsers not supporting the API
        folderInput.click();
    }
});

folderInput.addEventListener('change', () => {
    if (folderInput.files.length > 0) {
        selectedFilesCache = Array.from(folderInput.files);
        statusInput.innerText = `${folderInput.files.length} files selected`; // Updated from .value
    } else {
        statusInput.innerText = "No folder selected"; // Updated from .value
    }
});

// Helper to parse DICOM with robust fallback for headless/implicit files
function parseDicomBuffer(arrayBuffer) {
    try {
        return dcmjs.data.DicomMessage.readFile(arrayBuffer, { ignoreLeadingZeros: true, untilTag: null });
    } catch (e) {
        // Fallback: Construct a valid DICOM Part 10 File Meta Information header
        // Required elements: Preamble + DICM + (0002,0000) MetaGroupLength + (0002,0010) TransferSyntaxUID
        // This forces dcmjs to recognize the file and sets Transfer Syntax to Implicit VR Little Endian.

        const uidVal = "1.2.840.10008.1.2\0";
        const uidLen = uidVal.length; // 18 bytes
        
        // (0002,0010) Length: Tag(4) + VR(2) + Len(2) + Value(18) = 26 bytes
        const transferSyntaxSize = 26; 
        
        // (0002,0000) Length: Tag(4) + VR(2) + Len(2) + Value(4) = 12 bytes
        const metaInfoLengthSize = 12;
        
        const headerSize = 128 + 4 + metaInfoLengthSize + transferSyntaxSize;
        
        const newBuffer = new Uint8Array(arrayBuffer.byteLength + headerSize);
        let offset = 0;
        
        // 1. Preamble (128 bytes, all zeros)
        offset += 128;
        
        // 2. 'DICM' prefix
        newBuffer.set([68, 73, 67, 77], offset); 
        offset += 4;
        
        // 3. (0002,0000) File Meta Information Group Length
        // Tag: (0002,0000)
        newBuffer.set([0x02, 0x00, 0x00, 0x00], offset); offset += 4;
        // VR: 'UL'
        newBuffer.set([0x55, 0x4c], offset); offset += 2;
        // Length: 4
        newBuffer.set([0x04, 0x00], offset); offset += 2;
        // Value: Size of remaining Group 0002 elements (just Transfer Syntax here) = 26 bytes
        newBuffer.set([transferSyntaxSize, 0x00, 0x00, 0x00], offset); offset += 4; 

        // 4. (0002,0010) Transfer Syntax UID
        // Tag: (0002,0010)
        newBuffer.set([0x02, 0x00, 0x10, 0x00], offset); offset += 4;
        // VR: 'UI'
        newBuffer.set([0x55, 0x49], offset); offset += 2;
        // Length: 18 (0x0012)
        newBuffer.set([uidLen, 0x00], offset); offset += 2;
        // Value: "1.2.840.10008.1.2\0"
        for(let i=0; i<uidLen; i++) {
            newBuffer[offset+i] = uidVal.charCodeAt(i);
        }
        offset += uidLen;
        
        // 5. Append original headless dataset
        newBuffer.set(new Uint8Array(arrayBuffer), offset);
        
        return dcmjs.data.DicomMessage.readFile(newBuffer.buffer, { ignoreLeadingZeros: true, untilTag: null });
    }
}

async function handleScanFolder() {
    if (selectedFilesCache.length === 0) {
        showNotification("No Files Selected", "Please select a folder containing DICOM files.", "warning");
        return;
    }

    const files = selectedFilesCache;
    loadedDicomFiles = { ct: [], struct: null, dose: null };
    
    const btn = document.getElementById('scanBtn');
    btn.disabled = true;
    
    showLoading("Scanning files...");

    try {
        console.log(`Scanning ${files.length} files...`);
        
        for (const file of files) {
            try {
                const arrayBuffer = await readFileAsArrayBuffer(file);
                const dicomData = parseDicomBuffer(arrayBuffer);
                const dataset = dcmjs.data.DicomMetaDictionary.naturalizeDataset(dicomData.dict);
                
                const modality = dataset.Modality;
                if (modality === 'CT') {
                    loadedDicomFiles.ct.push(file);
                } else if (modality === 'RTSTRUCT') {
                    loadedDicomFiles.struct = file;
                } else if (modality === 'RTDOSE') {
                    loadedDicomFiles.dose = file;
                }
            } catch (e) {
                console.warn(`Skipping file ${file.name}: ${e.message}`);
            }
        }
        
        let msg = `Scan complete.\n` + 
                  `CT: ${loadedDicomFiles.ct.length}\n` + 
                  `RT Struct: ${loadedDicomFiles.struct ? "Found" : "Not Found"}\n` +
                  `RT Dose: ${loadedDicomFiles.dose ? "Found" : "Not Found"}`;
        
        // Show HTML formatted message
        Swal.fire({
            title: 'Scan Complete',
            html: `<div class="text-start">
                    <p class="mb-1"><i class="fa-solid fa-layer-group me-2 text-primary"></i>CT Images: <b>${loadedDicomFiles.ct.length}</b></p>
                    <p class="mb-1"><i class="fa-solid fa-shapes me-2 text-success"></i>RT Struct: <b>${loadedDicomFiles.struct ? "Found" : "Not Found"}</b></p>
                    <p class="mb-0"><i class="fa-solid fa-radiation me-2 text-warning"></i>RT Dose: <b>${loadedDicomFiles.dose ? "Found" : "Not Found"}</b></p>
                   </div>`,
            icon: 'success',
            confirmButtonColor: '#2563eb'
        });
        console.log("Loaded DICOMs:", loadedDicomFiles);

    } catch (e) {
        console.error(e);
        showNotification("Scan Error", "Error during scan: " + e.message, "error");
    } finally {
        btn.disabled = false;
        hideLoading();
    }
}

async function handleCalculation() {
    const prescribedDoseInput = document.getElementById('prescribedDose');
    
    if (!loadedDicomFiles.dose) {
        showNotification("No RT Dose", "No RT Dose file loaded. Please select a folder and click 'Scan Folder'.", "warning");
        return;
    }

    const file = loadedDicomFiles.dose;
    const prescribedDose = parseFloat(prescribedDoseInput.value) || 0;

    if (prescribedDose <= 0) {
        showNotification("Invalid Dose", "Please enter a valid prescribed dose", "warning");
        return;
    }

    // Switch to API mode
    // If you want to use local parsing, comment out checkServerAndProcess and uncomment processDicomData
    // await processLocally(file, prescribedDose);
    await processViaApi(file, prescribedDose);
}

async function processLocally(file, prescribedDose) {
    try {
        const arrayBuffer = await readFileAsArrayBuffer(file);
        processDicomData(arrayBuffer, prescribedDose);
    } catch (err) {
        console.error(err);
        showNotification("File Error", "Error reading or parsing file: " + err.message, "error");
    }
}

async function processViaApi(file, prescribedDose) {
    const btn = document.getElementById('calcBtn');
    
    // Helper: Build ImageSeriesInfo from CT Files if available, else fallback to Dose file
    let iop = null;
    let imagesInfo = [];
    
    // 1. Try to extract series info from Loaded CT files
    if (loadedDicomFiles.ct && loadedDicomFiles.ct.length > 0) {
        console.log(`Processing ${loadedDicomFiles.ct.length} CT files for ImageSeries...`);
        showLoading("Parsing CT files...");
        btn.disabled = true;

        try {
            // Process sequentially to avoid browser freeze on large sets
            for (let i = 0; i < loadedDicomFiles.ct.length; i++) {
                const ctFile = loadedDicomFiles.ct[i];
                if (i % 5 === 0) showLoading(`Parsing CT ${i+1}/${loadedDicomFiles.ct.length}...`);
                
                try {
                    const ab = await readFileAsArrayBuffer(ctFile);
                    const dicomData = parseDicomBuffer(ab);
                    const ds = dcmjs.data.DicomMetaDictionary.naturalizeDataset(dicomData.dict);
                    
                    // Capture IOP from the first CT that has it
                    if (!iop && ds.ImageOrientationPatient && ds.ImageOrientationPatient.length === 6) {
                        iop = ds.ImageOrientationPatient;
                    }

                    // Capture IPP from every CT
                    if (ds.ImagePositionPatient && ds.ImagePositionPatient.length === 3) {
                        imagesInfo.push({
                            imagePositionPatientInMm: ds.ImagePositionPatient
                        });
                    }
                } catch (err) {
                    console.warn(`Failed to parse CT header for ${ctFile.name}`, err);
                }
            }
        } catch(e) {
            console.error("Error during CT parsing loop", e);
        }
    }

    // 2. Fallback: If no CT info found, use the Dose file itself
    if (imagesInfo.length === 0) {
        console.warn("No CT series info found. Falling back to Dose file header.");
        let doseIpp = [0, 0, 0];
        try {
            const arrayBuffer = await readFileAsArrayBuffer(file);
            const dicomData = parseDicomBuffer(arrayBuffer);
            const dataset = dcmjs.data.DicomMetaDictionary.naturalizeDataset(dicomData.dict);
            
            if (dataset.ImageOrientationPatient) iop = dataset.ImageOrientationPatient;
            if (dataset.ImagePositionPatient) doseIpp = dataset.ImagePositionPatient;
        } catch (e) {
            console.warn("Could not parse DICOM header from Dose file:", e);
        }
        imagesInfo.push({ imagePositionPatientInMm: doseIpp });
    }

    // Default IOP if still missing
    if (!iop) iop = [1, 0, 0, 0, 1, 0];

    const formData = new FormData();
    // According to OpenAPI: dicomRtDose is the field name for the dose file
    formData.append('dicomRtDose', file); 
    
    let roisMetricInputs = [];

    if (loadedDicomFiles.struct) {
        formData.append('dicomRtStruct', loadedDicomFiles.struct);
        console.log("Attached RT Struct to request.");

        // Dynamically parse RT Struct to get ROIs for the request
        try {
            const ab = await readFileAsArrayBuffer(loadedDicomFiles.struct);
            const dicomData = parseDicomBuffer(ab);
            const ds = dcmjs.data.DicomMetaDictionary.naturalizeDataset(dicomData.dict);

            if (ds.StructureSetROISequence) {
                // Ensure it's an array (dcmjs might return object for single item)
                const seq = Array.isArray(ds.StructureSetROISequence) ? ds.StructureSetROISequence : [ds.StructureSetROISequence];
                
                roisMetricInputs = seq.map(roi => ({
                    roiId: roi.ROINumber,
                    roiName: roi.ROIName,
                    homogeneityIndexInput: {
                        highDoseRefInPercentage: 5,
                        minDoseRefInPercentage: 95
                    }, 
                    conformityIndexInput: {
                        doseOfInterestInCgy: prescribedDose * 100
                    }
                }));
                console.log(`Extracted ${roisMetricInputs.length} ROIs from RT Struct for API request.`);
            }
        } catch (e) {
            console.warn("Failed to parse ROIs from RT Struct file:", e);
        }

    } else {
        console.warn("No RT Struct file found in loaded context. API might reject request.");
    }
    
    // Constructing the JSON part (statisticsRequest) with REAL metadata
    const statisticsRequest = {
        imageSeries: {
            imageOrientationPatient: iop, 
            images: imagesInfo
        },
        dvhSettings: {
            binWidthInCgy: 5
        },
        roisMetricInputs: roisMetricInputs, 
        doseGradientIndexInput: {
            prescriptionDoseInCgy: prescribedDose * 100 
        }
    };

    formData.append('statisticsRequest', JSON.stringify(statisticsRequest));
    
    console.log("statisticsRequest:", statisticsRequest);

    showLoading("Calculating metrics...");

    try {
        // Updated endpoint to match your C# API, going through local proxy
        // Proxy forwards /api/plan-quality-metrics -> http://localhost/plan-quality-metrics
        const response = await fetch('/api/plan-quality-metrics/statistics', {
            method: 'POST',
            body: formData,
            headers: {
                'api-version': '1.0'
            }
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Server returned ${response.status} ${response.statusText}\n${errorText}`);
        }

        const result = await response.json();
        console.log("API Result:", result);
        cacheResponse.stats = result; // Cache the latest response
        renderApiResponse(result);
        showNotification("Success", "Analysis complete!", "success");

    } catch (err) {
        console.error("API Error:", err);
        showNotification(
            "API Failure",
            `API call failed: ${err.message}<br><small>(Please ensure <strong>node server.js</strong> is running)</small>`,
            "error"
        );
    } finally {
        btn.disabled = false;
        hideLoading();
    }
}

function renderApiResponse(response) {
    if (!response || !response.roiMetrics) return;

    const roiMetrics = response.roiMetrics;
    const chartDatasets = [];
    const tableBody = document.getElementById('metricsTableBody');
    tableBody.innerHTML = "";

    lastAnalysisResult = response; // Also update here in case called from elsewhere
    document.getElementById('evaluateBtn').disabled = false;
    document.getElementById('saveRequestBtn').disabled = false;
    
    // Reset Filter
    const searchInput = document.getElementById('roiSearchInput');
    if (searchInput) searchInput.value = '';

    // Helper for colors
    const colors = [
        '#FF6633', '#FFB399', '#FF33FF', '#FFFF99', '#00B3E6', 
        '#E6B333', '#3366E6', '#999966', '#99FF99', '#B34D4D',
        '#80B300', '#809900', '#E6B3B3', '#6680B3', '#66991A', 
        '#FF99E6', '#CCFF1A', '#FF1A66', '#E6331A', '#33FFCC',
        '#66994D', '#B366CC', '#4D8000', '#B33300', '#CC80CC', 
        '#66664D', '#991AFF', '#E666FF', '#4DB3FF', '#1AB399',
        '#E666B3', '#33991A', '#CC9999', '#B3B31A', '#00E680', 
        '#4D8066', '#809980', '#E6FF80', '#1AFF33', '#999933',
        '#FF3380', '#CCCC00', '#66E64D', '#4D80CC', '#9900B3', 
        '#E64D66', '#4DB380', '#FF4D4D', '#99E6E6', '#6666FF',
        // Additional 10 to reach 60
        '#FF9655', '#FFF79A', '#D783FF', '#CCFF00', '#1E90FF', 
        '#FF1493', '#32CD32', '#FF4500', '#8A2BE2', '#00CED1'
    ];

    let globalMaxDoseGy = 0;

    roiMetrics.forEach((roi, index) => {
        const color = colors[index % colors.length];
        
        // Convert metrics
        const volCm3 = (roi.volumeInMm3 || 0) / 1000.0;
        const minGy = (roi.minDoseInCgy || 0) / 100.0;
        const meanGy = (roi.meanDoseInCgy || 0) / 100.0;
        const maxGy = (roi.maxDoseInCgy || 0) / 100.0;
        const hi = roi.homogeneityIndex !== undefined && roi.homogeneityIndex !== null ? roi.homogeneityIndex.toFixed(2) : '-';
        const ci = roi.conformityIndex !== undefined && roi.conformityIndex !== null ? roi.conformityIndex.toFixed(2) : '-';

        // Update Global Max for Chart Scale
        if (maxGy > globalMaxDoseGy) globalMaxDoseGy = maxGy;

        // Table Row
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>
                <span class="badge rounded-pill me-2" style="background-color: ${color}">&nbsp;</span>
                ${roi.roiName || 'ROI ' + roi.roiId}
            </td>
            <td>${meanGy.toFixed(3)}</td>
            <td>${maxGy.toFixed(3)}</td>
            <td>${minGy.toFixed(3)}</td>
            <td>${volCm3.toFixed(3)}</td>
            <td>${hi}</td>
            <td>${ci}</td>
        `;
        tableBody.appendChild(row);

        // Chart Data
        if (roi.dvh && roi.dvh.cumulativeDvhs) {
            const dataPoints = roi.dvh.cumulativeDvhs.map(p => {
                const doseGy = p.d / 100.0;
                let volPerc = p.v;
                return { x: doseGy, y: volPerc };
            });

            chartDatasets.push({
                label: roi.roiName || 'ROI ' + roi.roiId,
                data: dataPoints,
                borderColor: color,
                backgroundColor: color.replace('rgb', 'rgba').replace(')', ', 0.1)'),
                pointRadius: 0,
                borderWidth: 2,
                tension: 0.1,
                showLine: true
            });
        }
    });

    updateChartWithLinearAxis(chartDatasets, globalMaxDoseGy);
}

function updateChartWithLinearAxis(datasets, maxDoseGy) {
    const ctx = document.getElementById('dvhChart').getContext('2d');
    
    if (dvhChartInstance) {
        dvhChartInstance.destroy();
    }

    dvhChartInstance = new Chart(ctx, {
        type: 'scatter',
        data: {
            datasets: datasets
        },
        options: {
            responsive: true,
            scales: {
                x: {
                    type: 'linear',
                    position: 'bottom',
                    title: {
                        display: true,
                        text: 'Dose (Gy)'
                    },
                    min: 0,
                    // Add 10% padding
                    max: maxDoseGy > 0 ? Math.ceil(maxDoseGy + 5) : undefined 
                },
                y: {
                    title: {
                        display: true,
                        text: 'Volume (%)'
                    },
                    min: 0,
                    max: 100
                }
            },
            plugins: {
                legend: {
                    position: 'top',
                },
                tooltip: {
                    mode: 'nearest',
                    intersect: false,
                    callbacks: {
                        label: function(context) {
                            let label = context.dataset.label || '';
                            if (label) {
                                label += ': ';
                            }
                            if (context.parsed.y !== null) {
                                label += context.parsed.y.toFixed(2) + '%';
                            }
                            if (context.parsed.x !== null) {
                                label += ` @ ${context.parsed.x.toFixed(2)} Gy`;
                            }
                            return label;
                        }
                    }
                }
            },
            interaction: {
                mode: 'nearest',
                axis: 'x',
                intersect: false
            }
        }
    });
}

function readFileAsArrayBuffer(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = (e) => resolve(e.target.result);
        reader.onerror = (e) => reject(e);
        reader.readAsArrayBuffer(file);
    });
}

function processDicomData(arrayBuffer, prescribedDose) {
    // Parse DICOM using dcmjs
    let dicomData;
    try {
        dicomData = parseDicomBuffer(arrayBuffer);
    } catch (e) {
        showNotification("Parse Error", "Unable to parse as DICOM file.", "error");
        return;
    }

    const dataset = dcmjs.data.DicomMetaDictionary.naturalizeDataset(dicomData.dict);

    if (dataset.Modality !== 'RTDOSE') {
        showNotification(
            "Modality Warning",
            `Warning: This file does not appear to be RT DOSE type (Modality: ${dataset.Modality}). Parsing errors may occur.`,
            "warning"
        );
    }

    // Extract necessary data
    const doseGridScaling = dataset.DoseGridScaling || 1.0;
    const pixelSpacing = dataset.PixelSpacing || [1, 1]; // row, col spacing in mm
    
    // Calculate Slice Thickness / Z-spacing
    let sliceThickness = dataset.SliceThickness || 1;
    if (dataset.GridFrameOffsetVector && dataset.GridFrameOffsetVector.length > 1) {
        // Assume minimal difference as thickness for uniform grids or just avg
        sliceThickness = Math.abs(dataset.GridFrameOffsetVector[1] - dataset.GridFrameOffsetVector[0]);
    }

    // Voxel volume in cm3 (mm3 -> /1000)
    // PixelSpacing is [RowSpacing, ColSpacing] -> [Y, X] usually, but usually they are similar.
    // Standard is [Row Spacing, Column Spacing]
    const voxelVolMm3 = pixelSpacing[0] * pixelSpacing[1] * sliceThickness;
    const voxelVolCm3 = voxelVolMm3 / 1000.0;

    // Get Pixel Data
    // PixelData is usually Int16 or UInt16. dcmjs might have it as an array or buffer.
    // We should handle the pixel representation.
    let pixelData = dataset.PixelData;
    
    // If it's a multi-frame buffer, we treat it as one large array
    // Convert all to absolute dose in Gy
    const doseValues = [];

    // Simple robust loop
    const len = pixelData.length;
    for (let i = 0; i < len; i++) {
        const rawVal = pixelData[i];
        if (rawVal < 0) continue; 
        const doseGy = rawVal * doseGridScaling;
        doseValues.push(doseGy);
    }

    renderAnalysis([{
        name: 'Local Dose File',
        color: 'rgb(75, 192, 192)',
        doseValues: doseValues,
        voxelVolCm3: voxelVolCm3
    }], prescribedDose);
}

// New function for criteria evaluation
async function handleCriteriaEvaluation() {
    if (!lastAnalysisResult) {
        showNotification("No Data", "Please run analysis first.", "warning");
        return;
    }

    const btn = document.getElementById('evaluateBtn');
    btn.disabled = true;
    showLoading("Evaluating Criteria...");

    try {
        const evaluationRequest = constructEvaluationRequest(lastAnalysisResult);
        console.log("Sending Evaluation Request:", evaluationRequest);

        // Proxy call to backend
        const response = await fetch('/api/plan-quality-metrics/criteria-evaluation', {
            method: 'POST',
            body: JSON.stringify(evaluationRequest),
            headers: {
                'Content-Type': 'application/json',
                'api-version': '1.0'
            }
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Server returned ${response.status}\n${errorText}`);
        }

        const evaluationResponse = await response.json();
        console.log("Evaluation Response:", evaluationResponse);
        
        // Dump to console as requested
        // showNotification("Evaluation Done", "Check console for details.", "success");

    } catch (err) {
        console.error("Evaluation Error:", err);
        showNotification("Evaluation Failed", err.message, "error");
    } finally {
        btn.disabled = false;
        hideLoading();
    }
}

function handleSaveEvalRequest() {
    if (!lastAnalysisResult) {
        showNotification("No Data", "Please run analysis first.", "warning");
        return;
    }

    try {
        const evaluationRequest = constructEvaluationRequest(lastAnalysisResult);
        const jsonStr = JSON.stringify(evaluationRequest, null, 2);
        
        // Setup download
        const blob = new Blob([jsonStr], { type: "application/json" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `evaluation_request_${new Date().getTime()}.json`;
        document.body.appendChild(a);
        a.click();
        
        // Cleanup
        setTimeout(() => {
            document.body.removeChild(a);
            window.URL.revokeObjectURL(url);
        }, 0);

        showNotification("Saved", "Request saved to JSON file.", "success");
    } catch (err) {
        console.error("Save Error:", err);
        showNotification("Error", "Failed to save request: " + err.message, "error");
    }
}

function constructEvaluationRequest(statistics) {
    // Generate mock criteria for ALL ROIs based on the provided C# class structure
    const mockCriteria = [];

    if (statistics.roiMetrics && Array.isArray(statistics.roiMetrics)) {
        statistics.roiMetrics.forEach(roi => {
            if (!roi.roiId) return;
            
            // Use crypto.randomUUID if available, else fallback
            const uuid = function () {
            if (typeof crypto !== 'undefined' && crypto.randomUUID) {
                return crypto.randomUUID();
            }
            return Math.random().toString(36).substring(2);
            };

            // 1. DoseMetricCriterion (e.g., Max Dose < 6000 cGy)
            mockCriteria.push({
                id: uuid(),
                roiId: roi.roiId,
                type: 'doseMetric',
                metric: 'maxDose', 
                thresholds: [{
                    kind: 'lessThanOrEqual',
                    valueInCgy: 6000,
                    toleranceInCgy: 0
                }]
            });

            // 2. DoseAtVolumeCriterion (e.g., D95% > 5000 cGy)
            mockCriteria.push({
                id: uuid(),
                roiId: roi.roiId,
                type: 'doseAtVolume',
                volumeValue: {
                    unit: 'percentage',
                    value: 95
                },
                doseThreshold: {
                    kind: 'lessThanOrEqual', // Just a mock check
                    valueInCgy: 5000,
                    toleranceInCgy: 100
                }
            });

            // 3. VolumeAtDoseCriterion (e.g., V20Gy < 50%)
            mockCriteria.push({
                id: uuid(),
                roiId: roi.roiId,
                type: 'volumeAtDose',
                doseInCgy: 2000,
                volumeThreshold: {
                    kind: 'lessThanOrEqual',
                    value: {
                        unit: 'percentage',
                        value: 50
                    },
                    tolerance: 2
                }
            });

            // 4. PlanIndexCriterion (e.g., Homogeneity Index < 1.1)
            mockCriteria.push({
                id: uuid(),
                roiId: roi.roiId,
                type: 'planIndex',
                indexType: 'homogeneityIndex',
                threshold: {
                    kind: 'lessThanOrEqual',
                    value: 1.1,
                    tolerance: 0.05
                }
            });
        });
    }

    return {
        statistics: statistics,
        criteria: mockCriteria
    };
}

function findDoseAtVolumePercent(volPercentArr, doseArr, targetPercent) {
    // Find where volume crosses targetPercent
    // Array is from Dose 0 -> Max. Volume starts at 100% (at 0 dose) and goes down.
    // volPercentArr[0] is roughly 100%. volPercentArr[max] is 0.
    // We want the dose where volume is targetPercent.
    
    // Find first index where vol < targetPercent
    for (let i = 0; i < volPercentArr.length; i++) {
        if (volPercentArr[i] < targetPercent) {
            // Linear interpolate between i-1 and i
            if (i === 0) return 0;
            const v1 = volPercentArr[i-1];
            const v2 = volPercentArr[i];
            const d1 = parseFloat(doseArr[i-1]);
            const d2 = parseFloat(doseArr[i]);
            
            // (target - v1) / (v2 - v1) = (dose - d1) / (d2 - d1)
            // dose = d1 + (d2 - d1) * (target - v1) / (v2 - v1)
            const fraction = (targetPercent - v1) / (v2 - v1);
            return d1 + (d2 - d1) * fraction;
        }
    }
    return parseFloat(doseArr[doseArr.length-1]);
}

function findVolumeAtDose(volArr, doseArr, targetDose) {
    // doseArr is increasing.
    // Find index where dose > targetDose
    for (let i = 0; i < doseArr.length; i++) {
        if (parseFloat(doseArr[i]) > targetDose) {
            if (i === 0) return volArr[0];
            const d1 = parseFloat(doseArr[i-1]);
            const d2 = parseFloat(doseArr[i]);
            const v1 = volArr[i-1];
            const v2 = volArr[i];
            
            const fraction = (targetDose - d1) / (d2 - d1);
            return v1 + (v2 - v1) * fraction;
        }
    }
    return 0;
}

function updateChart(labels, datasets) {
    const ctx = document.getElementById('dvhChart').getContext('2d');
    
    if (dvhChartInstance) {
        dvhChartInstance.destroy();
    }

    dvhChartInstance = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: datasets
        },
        options: {
            responsive: true,
            scales: {
                x: {
                    title: {
                        display: true,
                        text: 'Dose (Gy)'
                    }
                },
                y: {
                    title: {
                        display: true,
                        text: 'Volume (%)'
                    },
                    min: 0,
                    max: 100
                }
            },
            plugins: {
                legend: {
                    position: 'top',
                },
                tooltip: {
                    mode: 'index',
                    intersect: false,
                }
            },
            interaction: {
                mode: 'nearest',
                axis: 'x',
                intersect: false
            }
        }
    });
}

function loadDemoData() {
    const btn = document.getElementById('demoBtn');
    btn.disabled = true;
    showLoading("Generating demo data...");

    // Small timeout to allow UI to update
    setTimeout(() => {
        try {
            const prescribedDoseInput = document.getElementById('prescribedDose');
            const prescribedDose = parseFloat(prescribedDoseInput.value) || 60;
            const voxelVolCm3 = 0.008; 
            const count = 50000;

            // Helper to generate a distribution
            const generateROI = (name, color, meanShift, spread, tail) => {
                const values = [];
                for (let i = 0; i < count; i++) {
                    let dose;
                    let r = Math.random();
                    if (r < tail) {
                        // Tail / Scatter
                        dose = Math.random() * prescribedDose * 0.5;
                    } else {
                        // Main gaussian-ish peak
                        dose = (prescribedDose + meanShift) + (Math.random() - 0.5) * spread;
                    }
                    if (dose < 0) dose = 0;
                    values.push(dose);
                }
                return { name, color, doseValues: values, voxelVolCm3 };
            };

            const datasets = [
                generateROI("PTV_High", "rgb(255, 99, 132)", 2, 4, 0.05),      // Target: Red, near 62Gy
                generateROI("Bladder", "rgb(255, 205, 86)", -20, 15, 0.2),     // OAR: Yellow, around 40Gy
                generateROI("Rectum", "rgb(75, 192, 192)", -35, 10, 0.4),      // OAR: Green, around 25Gy
                generateROI("Body", "rgb(54, 162, 235)", -50, 20, 0.8)         // Body: Blue, low dose
            ];
            
            renderAnalysis(datasets, prescribedDose);
            showNotification("Demo Loaded", "Random demo data generated successfully.", "success");
        } catch (e) {
            console.error(e);
            showNotification("Error", "Failed to load demo data", "error");
        } finally {
            btn.disabled = false;
            hideLoading();
        }
    }, 100);
}

function renderAnalysis(dataGroups, prescribedDose) {
    if (!Array.isArray(dataGroups)) {
        console.error("renderAnalysis expects an array of ROI data objects");
        return;
    }

    // 1. Determine Global Max for X-Axis scaling
    let globalMaxDose = 0;
    dataGroups.forEach(g => {
        if (g.doseValues) {
            for (let d of g.doseValues) {
                if (d > globalMaxDose) globalMaxDose = d;
            }
        }
    });
    if (globalMaxDose === 0) globalMaxDose = 10; 

    const step = 0.05; 
    const numBins = Math.ceil(globalMaxDose / step) + 1;
    const doseLabels = Array.from({length: numBins}, (_, i) => (i * step).toFixed(2));

    // Clear Table
    const tableBody = document.getElementById('metricsTableBody');
    tableBody.innerHTML = "";

    const chartDatasets = [];

    // 2. Process each ROI
    dataGroups.forEach(group => {
        const { name, color, doseValues, voxelVolCm3 } = group;
        let count = doseValues.length;
        
        let min = Infinity, max = 0, sum = 0;
        const histogram = new Array(numBins).fill(0);

        for (let d of doseValues) {
            if (d < min) min = d;
            if (d > max) max = d;
            sum += d;
            
            const binIdx = Math.floor(d / step);
            if (binIdx < numBins) histogram[binIdx]++;
        }
        if (min === Infinity) min = 0;
        
        const mean = count > 0 ? sum / count : 0;
        const totalVol = count * voxelVolCm3;

        // Cumulative DVH
        let cumulativeVoxels = 0;
        const cumulativeVol = new Array(numBins).fill(0);
        for (let i = numBins - 1; i >= 0; i--) {
            cumulativeVoxels += histogram[i];
            cumulativeVol[i] = cumulativeVoxels * voxelVolCm3;
        }

        // Percent Volume
        const volPercent = totalVol > 0 ? cumulativeVol.map(v => (v / totalVol) * 100) : cumulativeVol.map(() => 0);

        // Metrics
        const d2 = findDoseAtVolumePercent(volPercent, doseLabels, 2);
        const d98 = findDoseAtVolumePercent(volPercent, doseLabels, 98);
        const d50 = findDoseAtVolumePercent(volPercent, doseLabels, 50);
        
        let hi = "-";
        if (d50 > 0) hi = ((d2 - d98) / d50).toFixed(3);

        // Add Table Row
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>
                <span class="badge rounded-pill me-2" style="background-color: ${color || '#6c757d'}">&nbsp;</span>
                ${name || 'Unknown'}
            </td>
            <td>${mean.toFixed(3)}</td>
            <td>${max.toFixed(3)}</td>
            <td>${min.toFixed(3)}</td>
            <td>${totalVol.toFixed(3)}</td>
            <td>${hi}</td>
            <td>-</td>
        `;
        tableBody.appendChild(row);

        // Add Chart Dataset
        chartDatasets.push({
            label: name,
            data: volPercent,
            borderColor: color || '#6c757d',
            backgroundColor: (color || '#6c757d').replace('rgb', 'rgba').replace(')', ', 0.1)'),
            pointRadius: 0,
            borderWidth: 2,
            tension: 0.1
        });
    });

    updateChart(doseLabels, chartDatasets);
}
