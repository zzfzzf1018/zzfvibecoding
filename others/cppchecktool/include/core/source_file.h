#pragma once

#include <string>
#include <vector>

namespace cppchecktool
{
struct SourceFile
{
    std::string path;
    std::string content;
    std::vector<std::string> lines;
};
}