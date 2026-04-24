cmake_minimum_required(VERSION 3.14)

include(FetchContent)

if(DEFINED ENV{CMAKE_TOOLCHAIN_FILE} AND NOT EXISTS "$ENV{CMAKE_TOOLCHAIN_FILE}")
    message(WARNING "Ignoring missing CMAKE_TOOLCHAIN_FILE from environment: $ENV{CMAKE_TOOLCHAIN_FILE}")
    unset(ENV{CMAKE_TOOLCHAIN_FILE})
endif()

if(DEFINED CMAKE_TOOLCHAIN_FILE AND NOT EXISTS "${CMAKE_TOOLCHAIN_FILE}")
    message(WARNING "Ignoring missing CMAKE_TOOLCHAIN_FILE from CMake cache: ${CMAKE_TOOLCHAIN_FILE}")
    unset(CMAKE_TOOLCHAIN_FILE CACHE)
    unset(CMAKE_TOOLCHAIN_FILE)
endif()

project(cppchecktool VERSION 0.1.0 LANGUAGES CXX)

option(CPPCHECKTOOL_FETCH_YAML_CPP "Fetch yaml-cpp automatically" ON)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

if(MSVC)
    add_compile_options(/W4 /permissive-)
endif()

if(CPPCHECKTOOL_FETCH_YAML_CPP)
    set(YAML_CPP_BUILD_TOOLS OFF CACHE BOOL "" FORCE)
    set(YAML_CPP_BUILD_TESTS OFF CACHE BOOL "" FORCE)
    set(YAML_CPP_BUILD_CONTRIB OFF CACHE BOOL "" FORCE)
    set(YAML_CPP_INSTALL OFF CACHE BOOL "" FORCE)
    FetchContent_Declare(
        yaml-cpp
        GIT_REPOSITORY https://github.com/jbeder/yaml-cpp.git
        GIT_TAG 0.8.0
    )
    FetchContent_MakeAvailable(yaml-cpp)
else()
    find_package(yaml-cpp CONFIG REQUIRED)
endif()

add_executable(cppchecktool
    src/backend/builtin_backend.cpp
    src/backend/cppcheck_backend.cpp
    src/core/config.cpp
    src/core/driver.cpp
    src/main.cpp
    src/report/report_writer.cpp
    src/rules/builtin_rules.cpp
)

target_include_directories(cppchecktool
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(cppchecktool
    PRIVATE
        yaml-cpp
)