#include <iostream>
#include <string>
#include <fstream>
#include <vector>
#include <sstream>

// VTK Includes
#include "vtkSmartPointer.h"
#include "vtkDICOMImageReader.h"
#include "vtkImageViewer2.h"
#include "vtkRenderWindow.h"
#include "vtkRenderWindowInteractor.h"
#include "vtkRenderer.h"
#include "vtkInteractorStyleImage.h"
#include "vtkTextActor.h"
#include "vtkTextProperty.h"
#include "vtkCallbackCommand.h"
#include "vtkCommand.h"
#include "vtkImageData.h"
#include "vtkImageMapToColors.h"
#include "vtkLookupTable.h"
#include "vtkImageActor.h"
#include "vtkImageMapper3D.h"
#include "vtkPointData.h"
#include "vtkPropPicker.h"
#include "vtkAssemblyPath.h"
#include "vtkCoordinate.h"
#include "vtkCursor2D.h"
#include "vtkPolyDataMapper2D.h"
#include "vtkProperty2D.h"
#include "vtkRegularPolygonSource.h"
#include "vtkActor2D.h"
#include "vtkPolyData.h"
#include "vtkCellArray.h"
#include "vtkPoints.h"
#include "vtkPolygon.h"
#include "vtkPolyDataMapper.h"
#include "vtkActor.h"
#include "vtkProperty.h"
#include "vtkLine.h"
#include "vtkMath.h" // Add vtkMath

#ifdef _WIN32
#include <windows.h>
#endif

// NFD Include
#include <nfd.h>

// Global variables
vtkSmartPointer<vtkDICOMImageReader> reader;
vtkSmartPointer<vtkImageViewer2> imageViewer;
vtkSmartPointer<vtkRenderWindowInteractor> renderWindowInteractor;
vtkSmartPointer<vtkImageData> maskImage;
vtkSmartPointer<vtkImageActor> maskActor;
vtkSmartPointer<vtkImageMapToColors> maskColorMapper;
vtkSmartPointer<vtkLookupTable> lookupTable;
vtkSmartPointer<vtkActor2D> brushCursorActor;

// Contour Visualization
vtkSmartPointer<vtkPoints> contourPoints;
vtkSmartPointer<vtkCellArray> contourLines;
vtkSmartPointer<vtkPolyData> contourPolyData;
vtkSmartPointer<vtkPolyDataMapper> contourMapper;
vtkSmartPointer<vtkActor> contourActor;

// Store points for saving/loading (World Coordinates)
struct Point3D {
    double x, y, z;
};
std::vector<Point3D> currentContourPoinst;

int brushSize = 2; // Thinner brush for "thin line" request
int currentSlice = 0;
int minSlice = 0;
int maxSlice = 0;

void InitializeContourVisualization() {
    contourPoints = vtkSmartPointer<vtkPoints>::New();
    contourLines = vtkSmartPointer<vtkCellArray>::New();
    contourPolyData = vtkSmartPointer<vtkPolyData>::New();
    
    contourPolyData->SetPoints(contourPoints);
    contourPolyData->SetLines(contourLines);
    
    contourMapper = vtkSmartPointer<vtkPolyDataMapper>::New();
    contourMapper->SetInputData(contourPolyData);
    
    contourActor = vtkSmartPointer<vtkActor>::New();
    contourActor->SetMapper(contourMapper);
    contourActor->GetProperty()->SetColor(0.0, 1.0, 0.0); // Green contour
    contourActor->GetProperty()->SetLineWidth(2.0);
    // User requested to remove green line visualization.
    // Changing to VisibilityOff so we still track points for saving but don't show the green line.
    contourActor->VisibilityOff();
}

void UpdateMaskVisibility() {
    if (maskActor) {
        maskActor->SetDisplayExtent(
            reader->GetOutput()->GetExtent()[0], reader->GetOutput()->GetExtent()[1],
            reader->GetOutput()->GetExtent()[2], reader->GetOutput()->GetExtent()[3],
            currentSlice, currentSlice
        );
    }
}

void InitializeMask(vtkImageData* input) {
    if (!input) return;

    maskImage = vtkSmartPointer<vtkImageData>::New();
    maskImage->CopyStructure(input);
    maskImage->AllocateScalars(VTK_UNSIGNED_CHAR, 1);
    
    // Initialize to 0 (transparent/black)
    void* scalarPointer = maskImage->GetScalarPointer();
    memset(scalarPointer, 0, maskImage->GetNumberOfPoints() * sizeof(unsigned char));

    // Create Color Map for the mask
    lookupTable = vtkSmartPointer<vtkLookupTable>::New();
    lookupTable->SetNumberOfTableValues(256);
    lookupTable->SetRange(0.0, 255.0);
    lookupTable->SetTableValue(0, 0.0, 0.0, 0.0, 0.0); // Transparent background
    
    // Ramp opacity for anti-aliasing
    // Values 1-255 map to Red with increasing opacity (up to 0.8)
    for (int i = 1; i < 256; i++) {
        double alpha = 0.8 * (static_cast<double>(i) / 255.0);
        // Using a non-linear ramp can look better for "solid core, soft edge"
        // alpha = 0.8 * pow(static_cast<double>(i) / 255.0, 2.0); 
        lookupTable->SetTableValue(i, 1.0, 0.0, 0.0, alpha); 
    }
    lookupTable->Build();

    maskColorMapper = vtkSmartPointer<vtkImageMapToColors>::New();
    maskColorMapper->SetLookupTable(lookupTable);
    maskColorMapper->SetInputData(maskImage);
    maskColorMapper->Update();

    maskActor = vtkSmartPointer<vtkImageActor>::New();
    maskActor->GetMapper()->SetInputConnection(maskColorMapper->GetOutputPort());
    
    // Initialize Brush Cursor (A circle following the mouse)
    vtkSmartPointer<vtkRegularPolygonSource> circle = vtkSmartPointer<vtkRegularPolygonSource>::New();
    circle->SetNumberOfSides(32);
    circle->SetRadius(brushSize);
    circle->SetCenter(0, 0, 0);

    vtkSmartPointer<vtkPolyDataMapper2D> mapper = vtkSmartPointer<vtkPolyDataMapper2D>::New();
    mapper->SetInputConnection(circle->GetOutputPort());
    
    brushCursorActor = vtkSmartPointer<vtkActor2D>::New();
    brushCursorActor->SetMapper(mapper);
    brushCursorActor->GetProperty()->SetColor(1.0, 1.0, 0.0); // Yellow
    brushCursorActor->GetProperty()->SetOpacity(0.5);
    brushCursorActor->VisibilityOff(); // Initially hidden until mouse enters
    
    // Initialize Contour Visualization
    InitializeContourVisualization();

    // We add it to the renderer
    imageViewer->GetRenderer()->AddActor2D(brushCursorActor);
    imageViewer->GetRenderer()->AddActor(maskActor);
    imageViewer->GetRenderer()->AddActor(contourActor);
}

// Forward declarations
void PaintLineSegmentWorld(double startWorld[3], double endWorld[3]);
void LoadContourPoints(const std::string& filename);

// Function to save contour points to file
void SaveContourPoints(const std::string& filename) {
    std::ofstream outfile(filename);
    if (!outfile.is_open()) {
        std::cerr << "Error opening file for writing: " << filename << std::endl;
        return;
    }
    
    outfile << currentContourPoinst.size() << std::endl;
    for (const auto& p : currentContourPoinst) {
        outfile << p.x << " " << p.y << " " << p.z << std::endl;
    }
    outfile.close();
    std::cout << "Saved " << currentContourPoinst.size() << " points to " << filename << std::endl;
}

// Function to load contour points from file
void LoadContourPoints(const std::string& filename) {
    std::ifstream infile(filename);
    if (!infile.is_open()) {
        std::cerr << "Error opening file for reading: " << filename << std::endl;
        return;
    }
    
    size_t count;
    infile >> count;
    
    currentContourPoinst.clear();
    contourPoints->Reset();
    contourLines->Reset();
    
    // We also need to decide which slice to show this on? 
    // Usually points are 3D, so they appear where they are.
    // However, if we want to "rasterize" them later, we might need Z.
    
    double x, y, z;
    vtkIdType prevId = -1;
    vtkIdType firstId = -1; // Restore vtkIdType
    Point3D prevP;
    bool hasPrev = false;
    Point3D firstP;
    
    // Clear mask before loading new contour?
    // void* scalarPointer = maskImage->GetScalarPointer();
    // memset(scalarPointer, 0, maskImage->GetNumberOfPoints() * sizeof(unsigned char));

    // Force Z of loaded points to current slice Z?
    // If we are working in 2D mode, loading a contour from another slice or invalid Z is annoying.
    // Let's adopt a strategy: If the points seem coplanar, we might want to shift them to current slice?
    // No, that's dangerous if it's a 3D structure.
    
    // Instead: Let's ensure that when we paint, we paint "thick" enough in Z to hit the slice?
    // No.
    
    // Let's assume the user wants to load into the CURRENT SLICE for editing.
    // So we override the Z of the loaded points to match the current slice Z.
    // This makes "Load" act like "Paste onto current slice".
    
    // Calculate current slice Z
    double origin[3]; 
    double spacing[3];
    maskImage->GetOrigin(origin);
    maskImage->GetSpacing(spacing);
    double currentZ = origin[2] + currentSlice * spacing[2];

    for (size_t i = 0; i < count; ++i) {
        infile >> x >> y >> z;
        
        // Override Z to current slice Z
        z = currentZ; 
        
        Point3D p = {x, y, z};
        currentContourPoinst.push_back(p);
        
        vtkIdType nextId = contourPoints->InsertNextPoint(x, y, z);
        if (hasPrev) {
            vtkSmartPointer<vtkLine> line = vtkSmartPointer<vtkLine>::New();
            line->GetPointIds()->SetId(0, prevId);
            line->GetPointIds()->SetId(1, nextId);
            contourLines->InsertNextCell(line);
            
            // Draw on Mask
             double p1[3] = {prevP.x, prevP.y, prevP.z};
             double p2[3] = {p.x, p.y, p.z};
             PaintLineSegmentWorld(p1, p2);
        } else {
            firstId = nextId;
            firstP = p;
        }
        prevId = nextId;
        prevP = p;
        hasPrev = true;
    }
    
    // Close the loop
    if (count > 2 && firstId != -1 && prevId != -1) {
        vtkSmartPointer<vtkLine> line = vtkSmartPointer<vtkLine>::New();
        line->GetPointIds()->SetId(0, prevId);
        line->GetPointIds()->SetId(1, firstId);
        contourLines->InsertNextCell(line);
        
        double p1[3] = {prevP.x, prevP.y, prevP.z};
        double p2[3] = {firstP.x, firstP.y, firstP.z};
        PaintLineSegmentWorld(p1, p2);
    }
    
    maskImage->Modified();
    maskColorMapper->Update();
    
    contourPolyData->Modified();
    imageViewer->Render();
    std::cout << "Loaded " << count << " points from " << filename << std::endl;
}

void RasterizeContourToMask() {
    if (currentContourPoinst.empty() || !maskImage) return;
    
    // 1. Create a logical polygon from current points
    // 2. Scan-fill or Use vtkPolygon::PointInPolygon for bounding box
    
    // Simple Bounding Box approach
    double bounds[6];
    contourPolyData->GetBounds(bounds);
    
    // Use the Z of the first point for the slice (assuming planar contour)
    double zWorld = currentContourPoinst[0].z;
    
    // Find Image Z index
    // Note: This assumes Z aligns with slices.
    int zIndex = currentSlice; // Fallback? 
    
    // Convert bounds to Image Coordinates to limit search
    double minWorld[3] = {bounds[0], bounds[2], zWorld};
    double maxWorld[3] = {bounds[1], bounds[3], zWorld};
    
    int minIJK[3], maxIJK[3];
    double pcoords[3];
    maskImage->ComputeStructuredCoordinates(minWorld, minIJK, pcoords);
    maskImage->ComputeStructuredCoordinates(maxWorld, maxIJK, pcoords);
    
    // Clamp to image dimensions
    int dims[3];
    maskImage->GetDimensions(dims);
    
    // Improve binding check vtkImageData::ComputeStructuredCoordinates might return 0 if out of bounds, 
    // so we manually clamp just in case.
    minIJK[0] = std::max(0, minIJK[0]); minIJK[1] = std::max(0, minIJK[1]);
    maxIJK[0] = std::min(dims[0]-1, maxIJK[0]); maxIJK[1] = std::min(dims[1]-1, maxIJK[1]);
    
    bool modified = false;
    
    double pixelWorld[3];
    int ijk[3];
    ijk[2] = currentSlice; // We rasterize to current slice
            
    // Prepare polygon for PointInPolygon check
    // We need 2D points (x, y)
    std::vector<double> flattenPoints;
    flattenPoints.reserve(currentContourPoinst.size() * 3);
    for (const auto& p : currentContourPoinst) {
            flattenPoints.push_back(p.x);
            flattenPoints.push_back(p.y);
            flattenPoints.push_back(0.0); // Flatten Z for 2D check
    }
    
    // Recalculate bounds for the flattened polygon
    double flatBounds[6] = {VTK_DOUBLE_MAX, VTK_DOUBLE_MIN, VTK_DOUBLE_MAX, VTK_DOUBLE_MIN, 0.0, 0.0};
    for (const auto& p : currentContourPoinst) {
        if (p.x < flatBounds[0]) flatBounds[0] = p.x;
        if (p.x > flatBounds[1]) flatBounds[1] = p.x;
        if (p.y < flatBounds[2]) flatBounds[2] = p.y;
        if (p.y > flatBounds[3]) flatBounds[3] = p.y;
    }

    double n[3] = {0,0,1}; // Normal (assuming axial slice)
    
    // Convert contour points to image space coordinates? 
    // No, PointInPolygon works on World Coords if we use World Coords for pixels.
    // That is fine.
    
    for (int j = minIJK[1]; j <= maxIJK[1]; j++) {
        for (int i = minIJK[0]; i <= maxIJK[0]; i++) {
            ijk[0] = i; ijk[1] = j;
            maskImage->GetPoint(maskImage->ComputePointId(ijk), pixelWorld);
            
            // Check if pixelWorld (x, y) is inside polygon
            double testPoint[3] = {pixelWorld[0], pixelWorld[1], 0.0};
            
            // Ignore Z for the test, use 2D check if possible or rely on projection
            if (vtkPolygon::PointInPolygon(testPoint, static_cast<int>(currentContourPoinst.size()), flattenPoints.data(), flatBounds, n)) {
                    unsigned char* pixel = static_cast<unsigned char*>(maskImage->GetScalarPointer(i, j, currentSlice));
                    if (*pixel == 0) {
                        *pixel = 1;
                        modified = true;
                    }
            }
        }
    }
    
    if (modified) {
        maskImage->Modified();
        maskColorMapper->Update();
        imageViewer->GetRenderWindow()->Render();
    }
}

void PaintPoint(int i, int j, int k, int dims[3], bool& modified) {
    // Squared radius for circular check
    int r2 = brushSize * brushSize;
    // ... Deprecated or used for other mode? 
    // Keeping logic if we want to mix modes later
    unsigned char* pixel = static_cast<unsigned char*>(maskImage->GetScalarPointer(i, j, k));
    *pixel = 1; 
    modified = true; 
}

void AddContourPoint(int x, int y) {
    if (!imageViewer->GetRenderer()) return;
    
    vtkSmartPointer<vtkRenderer> renderer = imageViewer->GetRenderer();
    double worldPoint[4];
    
    renderer->SetDisplayPoint(x, y, 0);
    renderer->DisplayToWorld();
    renderer->GetWorldPoint(worldPoint);
    
    // Store as 3D Point
    // Force Z to match current slice visualization logic if needed, 
    // but usually DisplayToWorld gives the focal plane or picked point.
    // Since we are using Image Actor, picking might be better, but DisplayToWorld on focal plane 
    // (which Image Viewer sets up) is usually sufficient for 2D viewers.
    
    // However, DisplayToWorld might give a Z that isn't exactly the slice Z if the camera is perspective or moved.
    // Let's force Z to the image slice Z for consistency in 2D mode.
    // Actually, vtkImageViewer2 usually sets up an Orthographic camera.
    // Let's rely on the picker or just use the Z from the image origin + slice spacing?
    // For now, let's trust DisplayToWorld but maybe clamp Z if it's way off.
    
    // UPDATE: To be safe for "3D points file", we should probably store what we see.
    // If we want to load it back, we need valid 3D coordinates.
    
    // Alternative: Use vtkPropPicker to pick the image pixel.
    vtkSmartPointer<vtkPropPicker> picker = vtkSmartPointer<vtkPropPicker>::New();
    if (picker->Pick(x, y, 0, renderer)) {
        picker->GetPickPosition(worldPoint);
    }
    
    // Add to vector
    currentContourPoinst.push_back({worldPoint[0], worldPoint[1], worldPoint[2]});
    
    // Add to VTK for visualization
    vtkIdType newId = contourPoints->InsertNextPoint(worldPoint[0], worldPoint[1], worldPoint[2]);
    
    if (newId > 0) {
        vtkSmartPointer<vtkLine> line = vtkSmartPointer<vtkLine>::New();
        line->GetPointIds()->SetId(0, newId - 1);
        line->GetPointIds()->SetId(1, newId);
        contourLines->InsertNextCell(line);
    }
    
    contourPolyData->Modified();
    imageViewer->GetRenderWindow()->Render();
}

void LoadDICOM(const std::string& folderPath) {
    if (folderPath.empty()) return;
    
    std::cout << "Loading DICOM from: " << folderPath << std::endl;
    
    reader->SetDirectoryName(folderPath.c_str());
    reader->Update();
    
    int* extent = reader->GetOutput()->GetExtent();
    minSlice = extent[4];
    maxSlice = extent[5];
    currentSlice = minSlice;

    imageViewer->SetInputConnection(reader->GetOutputPort());
    
    // Initialize Mask
    InitializeMask(reader->GetOutput());

    // Only Reset Camera ONCE on load, then respect user zoom
    imageViewer->GetRenderer()->ResetCamera();
    imageViewer->SetSlice(currentSlice);
    UpdateMaskVisibility();
    imageViewer->Render();
}

// Helper to paint a line segment in World Coordinates
void PaintLineSegmentWorld(double startWorld[3], double endWorld[3]) {
    if (!maskImage || !reader) return;

    // Line drawing logic with soft edges
    int steps = (int)(sqrt(vtkMath::Distance2BetweenPoints(startWorld, endWorld)) / (reader->GetOutput()->GetSpacing()[0] * 0.5));
    if (steps < 1) steps = 1;
    
    bool modified = false;
    int dims[3];
    maskImage->GetDimensions(dims);
    double spacing[3];
    maskImage->GetSpacing(spacing);
    double origin[3];
    maskImage->GetOrigin(origin);
    
    // Check if points are on current slice (roughly)
    // If we are loading 3D points, they might be on different slices.
    // For now we paint on the slice corresponding to the point's Z?
    // Or we just project to current slice if user wants to see them?
    // Let's assume we load points for the current slice or we handle 3D painting.
    // Given the simple nature, let's paint on the slice closest to the point Z.
    // But mask is 3D so we can paint in 3D.
    
    for (int i = 0; i <= steps; i++) {
        double t = (double)i / steps;
        double cx = startWorld[0] * (1.0-t) + endWorld[0] * t;
        double cy = startWorld[1] * (1.0-t) + endWorld[1] * t;
        double cz = startWorld[2] * (1.0-t) + endWorld[2] * t;
        
        // Z index
        // Using ComputeStructuredCoordinates for more robust Z-index finding
        // because manual calculation (cz - origin[2]) / spacing[2] fails if direction matrix is not identity
        // or if there are precision issues. 
   
        // For drawing, we primarily want to draw ON THE CURRENT SLICE if we are using 2D viewer.
        // If we trust the picker blindly, we might get a Z that is slightly off-slice (e.g. if we pick an actor slightly in front/behind).
        // Let's force the Z to be the current slice Z for the purpose of finding the slice index.
        // EXCEPT if we are loading points from file (LoadContourPoints), where we trust the Z.
        
        // This helper is used by both. 
        // Let's convert World to IJK completely.
        double worldPos[3] = {cx, cy, cz};
        int ijk[3];
        double pcoords[3];
        if (maskImage->ComputeStructuredCoordinates(worldPos, ijk, pcoords)) {
             // Valid point inside volume
             // But careful, ComputeStructuredCoordinates returns 1 if inside, 0 if outside.
             // Inside means we have valid ijk.
             
             // If we are drawing manually, we often want to force painting on currentSlice
             // even if the picked Z was slightly floating.
             // BUT LoadContour uses this too.
             
             // Check if 'sliceIdx' matches 'currentSlice' roughly?
             // Or just use the calculated 'ijk[2]'.
             
             int sliceIdx = ijk[2];
             
             // If manual tool (PaintLine) calls this, it gathers points via Picker.
             // If Picker picks the ImageActor, the Z should be correct for that slice.
             // If Picker misses or picks something else, Z might be off.
             
             // CRITICAL FIX: The previous manual calculation:
             // int sliceIdx = (int)((cz - origin[2]) / spacing[2] + 0.5);
             // fails if cz is slightly negative relative to origin, or rounding errors.
             // Using ComputeStructuredCoordinates is safer.
        } else {
             // Outside volume?
             // Try manual clamp if just slightly out
             int sliceIdx = (int)((cz - origin[2]) / spacing[2] + 0.5);
             if (sliceIdx < 0 || sliceIdx >= dims[2]) continue;
             
             // Calculate imgX, imgY manually
             // ...
        }

        // Reverting to a more robust hybrid approach
        // We calculate IJK from World using the image's own method
        double wPt[3] = {cx, cy, cz};
        int ijk_pt[3];
        double pcoords_pt[3];
        
        // We use ComputeStructuredCoordinates to get the cell, but we need point index.
        // Actually ComputeStructuredCoordinates returns the cell (voxel) index containing the point.
        // For painting on pixels (points), this is close enough.
        
        // ComputeStructuredCoordinates returns 1 if inside, 0 if outside.
        // If outside, ijk_pt might be garbage or clamped. The wrapper logic above suggested failure handling.
        // Let's rely on simple spacing math first but with better bounds check
        // Or re-implement ComputeStructuredCoordinates logic locally to be sure:
        // i = (x - origin[0]) / spacing[0]
        
        double imgX = (cx - origin[0]) / spacing[0];
        double imgY = (cy - origin[1]) / spacing[1];
        double imgZ = (cz - origin[2]) / spacing[2];
        
        int sliceIdx = (int)(imgZ + 0.5); // Round to nearest slice
        if (sliceIdx < 0 || sliceIdx >= dims[2]) {
             // Debug print only once per segment or suppress
             // std::cout << "Point out of bounds Z: " << cz << " slice: " << sliceIdx << std::endl;
             continue;
        }
        
        // Paint soft circle at (imgX, imgY)
        int centerI = (int)(imgX + 0.5);
        int centerJ = (int)(imgY + 0.5);
        
        // Search Radius logic
        int rWin = brushSize + 2; 
        
        for (int bY = -rWin; bY <= rWin; bY++) {
            for (int bX = -rWin; bX <= rWin; bX++) {
                 int pX = centerI + bX;
                 int pY = centerJ + bY;
                 
                 if (pX >= 0 && pX < dims[0] && pY >= 0 && pY < dims[1]) {
                     // Distance from true center (imgX, imgY)
                     double distSq = (pX - imgX)*(pX - imgX) + (pY - imgY)*(pY - imgY);
                     double radiusSq = brushSize * brushSize;
                     
                     double dist = sqrt(distSq);
                     double intensity = 0.0;
                     
                     // Solid core radius
                     double coreR = (double)brushSize - 0.5;
                     // Outer soft radius
                     double outerR = (double)brushSize + 0.5;
                     
                     if (dist <= coreR) {
                         intensity = 1.0;
                     } else if (dist <= outerR) {
                         intensity = 1.0 - (dist - coreR) / (outerR - coreR);
                     }
                     
                     if (intensity > 0.0) {
                         unsigned char* pixel = static_cast<unsigned char*>(maskImage->GetScalarPointer(pX, pY, sliceIdx));
                         int currentVal = *pixel;
                         int newVal = currentVal + (int)(intensity * 255.0 * 0.5); 
                         if (newVal > 255) newVal = 255;
                         if (newVal > currentVal) {
                             *pixel = (unsigned char)newVal; 
                             modified = true;
                         }
                     }
                 }
            }
        }
    }
}

// Implement PaintLine with Anti-Aliasing (Soft Brush)
void PaintLine(int x0, int y0, int x1, int y1) {
    if (!maskImage || !imageViewer->GetRenderer()) return;

    vtkRenderer* renderer = imageViewer->GetRenderer();
    double startWorld[4], endWorld[4];
    
    // Pick positions for accuracy
    vtkSmartPointer<vtkPropPicker> picker = vtkSmartPointer<vtkPropPicker>::New();
    
    if (picker->Pick(x0, y0, 0, renderer)) {
        picker->GetPickPosition(startWorld);
    } else {
        renderer->SetDisplayPoint(x0, y0, 0);
        renderer->DisplayToWorld();
        renderer->GetWorldPoint(startWorld);
    }

    if (picker->Pick(x1, y1, 0, renderer)) {
        picker->GetPickPosition(endWorld);
    } else {
        renderer->SetDisplayPoint(x1, y1, 0);
        renderer->DisplayToWorld();
        renderer->GetWorldPoint(endWorld);
    }
    
    // Call the World helper
    // FORCE Z to align with current slice
    // We assume the user wants to draw on the visible slice plane
    if (maskImage) {
        double origin[3]; 
        double spacing[3];
        maskImage->GetOrigin(origin);
        maskImage->GetSpacing(spacing);
        double zSlice = origin[2] + currentSlice * spacing[2];
        
        startWorld[2] = zSlice;
        endWorld[2] = zSlice;
    }
    
    PaintLineSegmentWorld(startWorld, endWorld);

    // Update render
    maskImage->Modified();
    maskColorMapper->Update();
    imageViewer->GetRenderWindow()->Render();
}


// Custom Interactor Style for Paintbrush and Slicing
class MouseInteractorStyle : public vtkInteractorStyleImage {
public:
    static MouseInteractorStyle* New() { return new MouseInteractorStyle; }

    void OnMouseWheelForward() override {
        if (!imageViewer || !imageViewer->GetInput()) return;
        if (currentSlice < maxSlice) {
            currentSlice++;
            imageViewer->SetSlice(currentSlice);
            UpdateMaskVisibility();
            // Don't call Render() on imageViewer directly here because it might reset camera
            imageViewer->GetRenderWindow()->Render(); 
        }
    }

    void OnMouseWheelBackward() override {
        if (!imageViewer || !imageViewer->GetInput()) return;
        if (currentSlice > minSlice) {
            currentSlice--;
            imageViewer->SetSlice(currentSlice);
            UpdateMaskVisibility();
            imageViewer->GetRenderWindow()->Render(); 
        }
    }
    
    void OnLeftButtonDown() override {
        if (!imageViewer || !imageViewer->GetInput()) return;
        this->Drawing = true;
        // Start new contour
        currentContourPoinst.clear();
        contourPoints->Reset();
        contourLines->Reset();
        contourPolyData->Modified();
        
        int* pos = this->GetInteractor()->GetEventPosition();
        
        // Don't visualize green line points? The user asked to remove "green line".
        // AddContourPoint adds to contourPoints which is the Green Actor.
        // We still need to record points for SAVING to file.
        // So we should modify AddContourPoint to NOT update the VTK Actor, or hide the Actor.
        
        AddContourPoint(pos[0], pos[1]);
        
        this->LastPos[0] = pos[0];
        this->LastPos[1] = pos[1];
        this->StartPos[0] = pos[0];
        this->StartPos[1] = pos[1];
        
        // Paint immediate point
        PaintLine(pos[0], pos[1], pos[0], pos[1]);
    }

    void OnLeftButtonUp() override {
        if (!imageViewer || !imageViewer->GetInput()) return;
        if (this->Drawing) {
            this->Drawing = false;
            
            // Close the loop for "contour" using PaintLine (draw line from LastPos to StartPos)
            PaintLine(this->LastPos[0], this->LastPos[1], this->StartPos[0], this->StartPos[1]);

            // User requested hollow contour (no fill)
            // RasterizeContourToMask(); // Fill the inside REMOVED
            
            // Refresh render
            imageViewer->GetRenderWindow()->Render();
        }
        vtkInteractorStyleImage::OnLeftButtonUp();
    }

    void OnMouseMove() override {
        int* pos = this->GetInteractor()->GetEventPosition();
        
        // Update Brush Cursor Position (Yellow Circle)
        if (brushCursorActor && imageViewer->GetRenderer()) {
            brushCursorActor->SetPosition(pos[0], pos[1]);
            brushCursorActor->VisibilityOn();
            imageViewer->GetRenderWindow()->Render();
        }

        if (this->Drawing && imageViewer && imageViewer->GetInput()) {
           AddContourPoint(pos[0], pos[1]);
           
           // Real-time Paint (Red Trail)
           PaintLine(this->LastPos[0], this->LastPos[1], pos[0], pos[1]);
           this->LastPos[0] = pos[0];
           this->LastPos[1] = pos[1];
           
        } else {
           vtkInteractorStyleImage::OnMouseMove();
        }
    }

    void OnLeave() override {
        if (brushCursorActor) {
            brushCursorActor->VisibilityOff();
            imageViewer->GetRenderWindow()->Render();
        }
    }

    void OnChar() override {
        vtkRenderWindowInteractor* rwi = this->GetInteractor();
        std::string key = rwi->GetKeySym();
        
        // Explicitly handle 'r' for reset because overriding OnChar often breaks default bind
        if (key == "r" || key == "R") {
            imageViewer->GetRenderer()->ResetCamera();
            imageViewer->Render();
            return;
        }

        if (key == "o" || key == "O") {
             nfdchar_t* outPath = NULL;
            nfdresult_t result = NFD_PickFolder(&outPath, NULL);
            if (result == NFD_OKAY) {
                // Ensure LoadDICOM is called deferred or handle properly, but it should remain valid here.
                // Crash might be due to VTK context if dialog interferes with window focus.
                // However, NFD is usually blocking.
                // Check if outPath is valid.
                if (outPath) {
                    LoadDICOM(outPath);
                    NFD_FreePath(outPath);
                }
            }
            return; // Don't pass 'o' to parent
        }
        
        if (key == "s" || key == "S") {
            nfdchar_t* savePath = NULL;
            const nfdu8filteritem_t filterItem[1] = { { "Text Files", "txt" } };
            nfdresult_t result = NFD_SaveDialog(&savePath, filterItem, 1, NULL, NULL);
            if (result == NFD_OKAY) {
                SaveContourPoints(savePath);
                NFD_FreePath(savePath);
            }
            return;
        }

        if (key == "l" || key == "L") {
            nfdchar_t* openPath = NULL;
            const nfdu8filteritem_t filterItem[1] = { { "Text Files", "txt" } };
            nfdresult_t result = NFD_OpenDialog(&openPath, filterItem, 1, NULL);
            if (result == NFD_OKAY) {
                LoadContourPoints(openPath);
                // RasterizeContourToMask(); // Removed because we now paint the outline in LoadContourPoints
                NFD_FreePath(openPath);
            }
            return;
        }
        
        // Forward other keys
        vtkInteractorStyleImage::OnChar();
    }

private:
    bool Drawing = false;
    int LastPos[2] = {0, 0};
    int StartPos[2] = {0, 0};
};

int main(int argc, char* argv[]) {
    // 1. Basic Setup
    NFD_Init();
    
    std::cout << "Starting CT Paint Tool..." << std::endl;

    // 2. Initialize VTK Objects
    reader = vtkSmartPointer<vtkDICOMImageReader>::New();
    imageViewer = vtkSmartPointer<vtkImageViewer2>::New();
    
    // 3. Setup Render Window and Interactor
    renderWindowInteractor = vtkSmartPointer<vtkRenderWindowInteractor>::New();
    imageViewer->SetupInteractor(renderWindowInteractor);
    
    // Custom Style
    vtkSmartPointer<MouseInteractorStyle> style = vtkSmartPointer<MouseInteractorStyle>::New();
    renderWindowInteractor->SetInteractorStyle(style);

    imageViewer->GetRenderer()->SetBackground(0.2, 0.2, 0.2); 
    
    // Add text instructions
    vtkSmartPointer<vtkTextActor> textActor = vtkSmartPointer<vtkTextActor>::New();
    textActor->SetInput("Controls:\n 'O': Open DICOM Folder\n 'S': Save Contour\n 'L': Load Contour\n Mouse Wheel: Scroll Slices\n Left Click & Drag: Draw Contour\n 'r': Reset Camera\n 'q': Quit");
    textActor->GetTextProperty()->SetFontSize(18);
    textActor->GetTextProperty()->SetColor(1.0, 1.0, 1.0);
    textActor->SetPosition(10, 10);
    imageViewer->GetRenderer()->AddActor2D(textActor);

    imageViewer->Render();
    
    // 4. Start
    renderWindowInteractor->Initialize();
    
    // Maximize Window on Windows
#ifdef _WIN32
    vtkRenderWindow* renWin = imageViewer->GetRenderWindow();
    // Ensure the window is created/realized before getting the handle
    if (renWin->GetGenericWindowId()) {
        HWND hWnd = reinterpret_cast<HWND>(renWin->GetGenericWindowId());
        ShowWindow(hWnd, SW_MAXIMIZE);
    }
#endif
    
    // Ensure image fills the screen
    imageViewer->GetRenderer()->ResetCamera();
    
    renderWindowInteractor->Start();

    NFD_Quit();
    return 0;
}
