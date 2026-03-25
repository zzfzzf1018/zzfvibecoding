cmake_minimum_required(VERSION 3.20)
project(MfcAsyncAwaitDemo LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_MFC_FLAG 2)

add_executable(MfcAsyncAwaitDemo WIN32
    src/main.cpp
    src/MainFrame.h
    src/MainFrame.cpp
    src/AsyncAwait.h
)

target_compile_definitions(MfcAsyncAwaitDemo PRIVATE
    _AFXDLL
    _WIN32_WINNT=0x0A00
    UNICODE
    _UNICODE
    WIN32
    _WINDOWS
)

if (MSVC)
    target_compile_options(MfcAsyncAwaitDemo PRIVATE /utf-8)
endif()
