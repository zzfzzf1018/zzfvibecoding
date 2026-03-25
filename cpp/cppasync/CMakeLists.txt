cmake_minimum_required(VERSION 3.8)
project(AsyncDemo)

# 确保只在 MSVC 下添加特定标志
if(MSVC)
    # /await 启用协程支持 (VS 2017/2019 必需)
    # /EHsc 启用标准 C++ 异常处理
    # /W4 开启高等级警告
    add_compile_options(/await /EHsc /W4)
endif()

# 设置 C++ 标准
set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_executable(async_demo async_demo.cpp)