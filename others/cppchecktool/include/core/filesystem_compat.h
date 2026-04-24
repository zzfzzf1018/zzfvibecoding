#pragma once

#if defined(__has_include)
#if __has_include(<filesystem>)
#include <filesystem>
namespace cppchecktool
{
namespace fs = std::filesystem;
}
#else
#include <experimental/filesystem>
namespace cppchecktool
{
namespace fs = std::experimental::filesystem;
}
#endif
#else
#include <experimental/filesystem>
namespace cppchecktool
{
namespace fs = std::experimental::filesystem;
}
#endif