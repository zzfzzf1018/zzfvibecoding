#pragma once

#include <string>
#include <vector>

namespace cppchecktool
{
enum class OutputFormat
{
    Text,
    Sarif
};

enum class BackendMode
{
    Builtin,
    Cppcheck,
    Both,
    Auto
};

struct ScanOptions
{
    std::string targetPath = ".";
    std::string outputPath;
    std::string configPath;
    std::string compileDatabasePath;
    std::string cppcheckPath = "cppcheck";
    std::string cppcheckEnable = "warning,style,performance,portability,information";
    OutputFormat outputFormat = OutputFormat::Text;
    BackendMode backendMode = BackendMode::Auto;
    bool recursive = true;
    std::vector<std::string> excludedPaths;
    std::vector<std::string> extensions;
};

inline const char* ToString(const OutputFormat format)
{
    switch (format)
    {
    case OutputFormat::Text:
        return "text";
    case OutputFormat::Sarif:
        return "sarif";
    }

    return "text";
}

inline const char* ToString(const BackendMode mode)
{
    switch (mode)
    {
    case BackendMode::Builtin:
        return "builtin";
    case BackendMode::Cppcheck:
        return "cppcheck";
    case BackendMode::Both:
        return "both";
    case BackendMode::Auto:
        return "auto";
    }

    return "auto";
}
}