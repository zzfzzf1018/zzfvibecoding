cmake_minimum_required(VERSION 3.16)

project(CTContouringTool VERSION 1.0)

# Specify the C++ standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED True)

# Find ITK
find_package(ITK REQUIRED)
include(${ITK_USE_FILE})

# Find VTK
find_package(VTK REQUIRED)

# Find nativefiledialog-extended (package name is nfd)
find_package(nfd CONFIG REQUIRED)

# Add the executable
add_executable(CTContouringTool src/main.cpp)

# Link libraries
target_link_libraries(CTContouringTool PRIVATE ${ITK_LIBRARIES} ${VTK_LIBRARIES} nfd::nfd)

# Basic include directories
target_include_directories(CTContouringTool PRIVATE src)
