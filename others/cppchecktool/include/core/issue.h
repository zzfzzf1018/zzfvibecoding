#pragma once

#include <cstddef>
#include <string>

namespace cppchecktool
{
enum class Severity
{
    Note,
    Warning,
    Error
};

enum class Category
{
    Security,
    Memory,
    Concurrency,
    Maintainability,
    Platform,
    Reliability
};

struct Issue
{
    std::string ruleId;
    std::string engine;
    Severity severity = Severity::Warning;
    Category category = Category::Reliability;
    std::string message;
    std::string suggestion;
    std::string filePath;
    std::size_t line = 0;
    std::size_t column = 0;
};

inline const char* ToString(const Severity severity)
{
    switch (severity)
    {
    case Severity::Note:
        return "note";
    case Severity::Warning:
        return "warning";
    case Severity::Error:
        return "error";
    }

    return "warning";
}

inline const char* ToString(const Category category)
{
    switch (category)
    {
    case Category::Security:
        return "security";
    case Category::Memory:
        return "memory";
    case Category::Concurrency:
        return "concurrency";
    case Category::Maintainability:
        return "maintainability";
    case Category::Platform:
        return "platform";
    case Category::Reliability:
        return "reliability";
    }

    return "reliability";
}
}