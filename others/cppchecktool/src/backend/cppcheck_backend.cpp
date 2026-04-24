#include "backend/backend.h"

#include <algorithm>
#include <array>
#include <cstdlib>
#include <cstdio>
#include <sstream>

#include "core/filesystem_compat.h"

namespace cppchecktool
{
namespace
{
std::string QuoteArgument(const std::string& value)
{
    if (value.find_first_of(" \t\"") == std::string::npos)
    {
        return value;
    }

    std::string escaped = "\"";
    for (std::string::const_iterator it = value.begin(); it != value.end(); ++it)
    {
        if (*it == '"')
        {
            escaped += '\\';
        }
        escaped += *it;
    }
    escaped += '"';
    return escaped;
}

bool FileExists(const std::string& path)
{
    return !path.empty() && fs::exists(fs::path(path));
}

std::string GetEnvironmentValue(const char* key)
{
#if defined(_WIN32)
    char* buffer = NULL;
    std::size_t length = 0;
    if (_dupenv_s(&buffer, &length, key) != 0 || buffer == NULL)
    {
        return std::string();
    }

    std::string value(buffer);
    free(buffer);
    return value;
#else
    const char* value = std::getenv(key);
    return value == NULL ? std::string() : std::string(value);
#endif
}

std::string JoinPath(const char* basePath, const char* suffix)
{
    if (basePath == NULL || *basePath == '\0')
    {
        return std::string();
    }

    return (fs::path(basePath) / fs::path(suffix)).lexically_normal().string();
}

std::string ResolveCppcheckExecutable(const std::string& configuredPath)
{
    if (configuredPath.find('/') != std::string::npos || configuredPath.find('\\') != std::string::npos)
    {
        return configuredPath;
    }

    if (FileExists(configuredPath))
    {
        return configuredPath;
    }

#if defined(_WIN32)
    const std::string programFilesValue = GetEnvironmentValue("ProgramFiles");
    const std::string programFilesPath = JoinPath(programFilesValue.c_str(), "Cppcheck/cppcheck.exe");
    if (FileExists(programFilesPath))
    {
        return programFilesPath;
    }

    const std::string programFilesX86Value = GetEnvironmentValue("ProgramFiles(x86)");
    const std::string programFilesX86Path = JoinPath(programFilesX86Value.c_str(), "Cppcheck/cppcheck.exe");
    if (FileExists(programFilesX86Path))
    {
        return programFilesX86Path;
    }
#endif

    return configuredPath;
}

bool RunCommand(const std::string& command, std::string& output)
{
#if defined(_WIN32)
    FILE* pipe = _popen(command.c_str(), "r");
#else
    FILE* pipe = popen(command.c_str(), "r");
#endif
    if (pipe == NULL)
    {
        return false;
    }

    std::array<char, 512> buffer;
    while (fgets(buffer.data(), static_cast<int>(buffer.size()), pipe) != NULL)
    {
        output.append(buffer.data());
    }

#if defined(_WIN32)
    const int exitCode = _pclose(pipe);
#else
    const int exitCode = pclose(pipe);
#endif
    return exitCode == 0;
}

std::vector<std::string> SplitLine(const std::string& line)
{
    std::vector<std::string> parts;
    const std::string separator = "@@@";
    std::string::size_type start = 0;
    while (start <= line.size())
    {
        const std::string::size_type position = line.find(separator, start);
        if (position == std::string::npos)
        {
            parts.push_back(line.substr(start));
            break;
        }

        parts.push_back(line.substr(start, position - start));
        start = position + separator.size();
    }

    return parts;
}

Severity MapSeverity(const std::string& value)
{
    if (value == "error")
    {
        return Severity::Error;
    }
    if (value == "information" || value == "style")
    {
        return Severity::Note;
    }
    return Severity::Warning;
}

Category MapCategory(const std::string& ruleId, const std::string& severity)
{
    if (ruleId.find("mem") != std::string::npos || ruleId.find("null") != std::string::npos)
    {
        return Category::Memory;
    }
    if (ruleId.find("thread") != std::string::npos || ruleId.find("race") != std::string::npos)
    {
        return Category::Concurrency;
    }
    if (severity == "portability")
    {
        return Category::Platform;
    }
    if (severity == "style")
    {
        return Category::Maintainability;
    }
    return Category::Reliability;
}

bool ParseIssueLine(const std::string& line, Issue& issue)
{
    const std::vector<std::string> parts = SplitLine(line);
    if (parts.size() < 6)
    {
        return false;
    }

    issue.filePath = parts[0];
    if (issue.filePath.empty() || issue.filePath == "nofile")
    {
        return false;
    }
    issue.line = static_cast<std::size_t>(std::strtoul(parts[1].c_str(), NULL, 10));
    issue.column = static_cast<std::size_t>(std::strtoul(parts[2].c_str(), NULL, 10));
    issue.severity = MapSeverity(parts[3]);
    issue.category = MapCategory(parts[4], parts[3]);
    issue.ruleId = "CPPCHECK-" + parts[4];
    issue.message = parts[5];
    issue.suggestion = "review the cppcheck diagnostic in project context";
    issue.engine = "cppcheck";
    return !issue.filePath.empty();
}

class CppcheckBackend : public Backend
{
public:
    const char* Name() const override
    {
        return "cppcheck";
    }

    bool Analyze(const AnalysisContext& context, std::vector<Issue>& issues, std::string& errorMessage) const override
    {
        const std::string cppcheckExecutable = ResolveCppcheckExecutable(context.options.cppcheckPath);
        std::ostringstream command;
        command << QuoteArgument(cppcheckExecutable) << " --quiet";
        command << " --inline-suppr";
        command << " --enable=" << QuoteArgument(context.options.cppcheckEnable);
        command << " --template=" << QuoteArgument("{file}@@@{line}@@@{column}@@@{severity}@@@{id}@@@{message}");

        if (!context.options.compileDatabasePath.empty())
        {
            command << " --project=" << QuoteArgument(context.options.compileDatabasePath);
        }
        else
        {
            for (std::vector<SourceFile>::const_iterator it = context.sourceFiles.begin(); it != context.sourceFiles.end(); ++it)
            {
                command << ' ' << QuoteArgument(it->path);
            }
        }

        command << " 2>&1";

        std::string output;
        if (!RunCommand(command.str(), output))
        {
            errorMessage = output.empty() ? "failed to run cppcheck backend" : output;
            return false;
        }

        std::istringstream stream(output);
        std::string line;
        while (std::getline(stream, line))
        {
            if (!line.empty() && line.back() == '\r')
            {
                line.pop_back();
            }

            Issue issue;
            if (ParseIssueLine(line, issue))
            {
                issues.push_back(issue);
            }
        }

        return true;
    }
};
}

bool IsCppcheckAvailable(const ScanOptions& options, std::string& versionText)
{
    const std::string cppcheckExecutable = ResolveCppcheckExecutable(options.cppcheckPath);
    std::string command = QuoteArgument(cppcheckExecutable) + " --version 2>&1";
    return RunCommand(command, versionText);
}

std::unique_ptr<Backend> CreateCppcheckBackend()
{
    return std::unique_ptr<Backend>(new CppcheckBackend());
}
}