#include "core/driver.h"

#include <algorithm>
#include <cctype>
#include <fstream>
#include <memory>
#include <sstream>
#include <utility>

#include "backend/backend.h"
#include "core/filesystem_compat.h"
#include "core/source_file.h"

namespace cppchecktool
{
namespace
{
std::string ToLowerCopy(const std::string& value)
{
    std::string result = value;
    std::transform(result.begin(), result.end(), result.begin(), [](unsigned char ch) {
        return static_cast<char>(std::tolower(ch));
    });
    return result;
}

std::vector<std::string> BuildExtensions(const ScanOptions& options)
{
    if (!options.extensions.empty())
    {
        std::vector<std::string> lowered = options.extensions;
        std::transform(lowered.begin(), lowered.end(), lowered.begin(), ToLowerCopy);
        return lowered;
    }

    return {".c", ".cc", ".cpp", ".cxx", ".h", ".hh", ".hpp", ".hxx", ".inl"};
}

bool IsSourceExtension(const fs::path& filePath, const std::vector<std::string>& extensions)
{
    const std::string extension = ToLowerCopy(filePath.extension().string());
    return std::find(extensions.begin(), extensions.end(), extension) != extensions.end();
}

fs::path NormalizePath(const fs::path& filePath)
{
    return filePath.lexically_normal();
}

bool IsExcluded(const fs::path& filePath, const ScanOptions& options)
{
    const std::string normalizedPath = ToLowerCopy(NormalizePath(filePath).generic_string());
    for (std::vector<std::string>::const_iterator it = options.excludedPaths.begin(); it != options.excludedPaths.end(); ++it)
    {
        const std::string excluded = ToLowerCopy(*it);
        if (!excluded.empty() && normalizedPath.find(excluded) != std::string::npos)
        {
            return true;
        }
    }

    return false;
}

bool ReadFileText(const fs::path& filePath, std::string& text, std::string& errorMessage)
{
    std::ifstream stream(filePath.string().c_str(), std::ios::in | std::ios::binary);
    if (!stream)
    {
        errorMessage = "failed to read file: " + filePath.string();
        return false;
    }

    std::ostringstream buffer;
    buffer << stream.rdbuf();
    text = buffer.str();
    return true;
}

bool ParseJsonString(const std::string& text, std::string::size_type& position, std::string& value)
{
    if (position >= text.size() || text[position] != '"')
    {
        return false;
    }

    ++position;
    value.clear();
    while (position < text.size())
    {
        const char current = text[position++];
        if (current == '"')
        {
            return true;
        }
        if (current == '\\' && position < text.size())
        {
            const char escaped = text[position++];
            switch (escaped)
            {
            case '"':
            case '\\':
            case '/':
                value += escaped;
                break;
            case 'b':
                value += '\b';
                break;
            case 'f':
                value += '\f';
                break;
            case 'n':
                value += '\n';
                break;
            case 'r':
                value += '\r';
                break;
            case 't':
                value += '\t';
                break;
            default:
                value += escaped;
                break;
            }
            continue;
        }
        value += current;
    }

    return false;
}

std::vector<std::string> ExtractJsonObjects(const std::string& content)
{
    std::vector<std::string> objects;
    bool inString = false;
    bool escaping = false;
    int depth = 0;
    std::string::size_type start = std::string::npos;

    for (std::string::size_type index = 0; index < content.size(); ++index)
    {
        const char current = content[index];
        if (inString)
        {
            if (escaping)
            {
                escaping = false;
            }
            else if (current == '\\')
            {
                escaping = true;
            }
            else if (current == '"')
            {
                inString = false;
            }
            continue;
        }

        if (current == '"')
        {
            inString = true;
            continue;
        }

        if (current == '{')
        {
            if (depth == 0)
            {
                start = index;
            }
            ++depth;
            continue;
        }

        if (current == '}')
        {
            --depth;
            if (depth == 0 && start != std::string::npos)
            {
                objects.push_back(content.substr(start, index - start + 1));
                start = std::string::npos;
            }
        }
    }

    return objects;
}

bool ExtractJsonStringField(const std::string& object, const std::string& key, std::string& value)
{
    const std::string quotedKey = "\"" + key + "\"";
    std::string::size_type keyPosition = object.find(quotedKey);
    if (keyPosition == std::string::npos)
    {
        return false;
    }

    std::string::size_type colonPosition = object.find(':', keyPosition + quotedKey.size());
    if (colonPosition == std::string::npos)
    {
        return false;
    }

    ++colonPosition;
    while (colonPosition < object.size() && std::isspace(static_cast<unsigned char>(object[colonPosition])) != 0)
    {
        ++colonPosition;
    }

    return ParseJsonString(object, colonPosition, value);
}

std::vector<fs::path> CollectFilesFromCompileDatabase(const ScanOptions& options, std::string& errorMessage)
{
    std::string content;
    if (!ReadFileText(fs::path(options.compileDatabasePath), content, errorMessage))
    {
        return {};
    }

    const std::vector<std::string> objects = ExtractJsonObjects(content);
    const std::vector<std::string> extensions = BuildExtensions(options);
    std::vector<fs::path> files;

    for (std::vector<std::string>::const_iterator it = objects.begin(); it != objects.end(); ++it)
    {
        std::string fileValue;
        if (!ExtractJsonStringField(*it, "file", fileValue))
        {
            continue;
        }

        std::string directoryValue;
        const bool hasDirectory = ExtractJsonStringField(*it, "directory", directoryValue);

        fs::path resolvedPath(fileValue);
        if (resolvedPath.is_relative() && hasDirectory)
        {
            resolvedPath = fs::path(directoryValue) / resolvedPath;
        }

        resolvedPath = NormalizePath(resolvedPath);
        if (IsExcluded(resolvedPath, options) || !IsSourceExtension(resolvedPath, extensions))
        {
            continue;
        }

        files.push_back(resolvedPath);
    }

    std::sort(files.begin(), files.end());
    files.erase(std::unique(files.begin(), files.end()), files.end());
    return files;
}

std::vector<fs::path> CollectFilesFromPath(const ScanOptions& options, std::string& errorMessage)
{
    const fs::path targetPath(options.targetPath);
    if (!fs::exists(targetPath))
    {
        errorMessage = "target path does not exist: " + options.targetPath;
        return {};
    }

    const std::vector<std::string> extensions = BuildExtensions(options);
    std::vector<fs::path> files;

    if (fs::is_regular_file(targetPath))
    {
        if (IsSourceExtension(targetPath, extensions))
        {
            files.push_back(targetPath);
        }
        return files;
    }

    if (options.recursive)
    {
        for (fs::recursive_directory_iterator it(targetPath), end; it != end; ++it)
        {
            if (it->is_directory() && IsExcluded(it->path(), options))
            {
                it.disable_recursion_pending();
                continue;
            }

            if (!it->is_regular_file())
            {
                continue;
            }

            if (IsExcluded(it->path(), options))
            {
                continue;
            }

            if (IsSourceExtension(it->path(), extensions))
            {
                files.push_back(it->path());
            }
        }
    }
    else
    {
        for (fs::directory_iterator it(targetPath), end; it != end; ++it)
        {
            if (it->is_regular_file() && !IsExcluded(it->path(), options) && IsSourceExtension(it->path(), extensions))
            {
                files.push_back(it->path());
            }
        }
    }

    return files;
}

bool LoadSourceFile(const fs::path& filePath, SourceFile& sourceFile, std::string& errorMessage)
{
    if (!ReadFileText(filePath, sourceFile.content, errorMessage))
    {
        return false;
    }

    sourceFile.path = NormalizePath(filePath).generic_string();

    std::istringstream lineStream(sourceFile.content);
    std::string line;
    while (std::getline(lineStream, line))
    {
        if (!line.empty() && line.back() == '\r')
        {
            line.pop_back();
        }
        sourceFile.lines.push_back(line);
    }

    if (!sourceFile.content.empty() && sourceFile.content.back() == '\n' && sourceFile.lines.empty())
    {
        sourceFile.lines.push_back(std::string());
    }

    return true;
}

std::vector<SourceFile> LoadSourceFiles(const std::vector<fs::path>& files, std::string& errorMessage)
{
    std::vector<SourceFile> sourceFiles;
    sourceFiles.reserve(files.size());
    for (std::vector<fs::path>::const_iterator it = files.begin(); it != files.end(); ++it)
    {
        SourceFile sourceFile;
        if (!LoadSourceFile(*it, sourceFile, errorMessage))
        {
            return {};
        }
        sourceFiles.push_back(sourceFile);
    }
    return sourceFiles;
}

bool LineContainsSuppression(const std::string& line, const std::string& ruleId)
{
    const std::string marker = "check-ignore:";
    const std::size_t markerPosition = line.find(marker);
    if (markerPosition == std::string::npos)
    {
        return false;
    }

    const std::string payload = ToLowerCopy(line.substr(markerPosition + marker.size()));
    return payload.find("all") != std::string::npos || payload.find(ToLowerCopy(ruleId)) != std::string::npos;
}

bool IsSuppressed(const SourceFile& sourceFile, const Issue& issue)
{
    if (issue.line == 0 || sourceFile.lines.empty())
    {
        return false;
    }

    const std::size_t lineIndex = issue.line - 1;
    if (lineIndex < sourceFile.lines.size() && LineContainsSuppression(sourceFile.lines[lineIndex], issue.ruleId))
    {
        return true;
    }

    if (lineIndex > 0 && LineContainsSuppression(sourceFile.lines[lineIndex - 1], issue.ruleId))
    {
        return true;
    }

    return false;
}

const SourceFile* FindSourceFile(const std::vector<SourceFile>& sourceFiles, const std::string& filePath)
{
    for (std::vector<SourceFile>::const_iterator it = sourceFiles.begin(); it != sourceFiles.end(); ++it)
    {
        if (it->path == filePath)
        {
            return &(*it);
        }
    }
    return NULL;
}
}

ScanResult Driver::Scan(const ScanOptions& options, std::string& errorMessage) const
{
    ScanResult result;
    std::vector<fs::path> files;
    if (!options.compileDatabasePath.empty())
    {
        files = CollectFilesFromCompileDatabase(options, errorMessage);
    }
    else
    {
        files = CollectFilesFromPath(options, errorMessage);
    }

    if (!errorMessage.empty())
    {
        return result;
    }

    std::vector<SourceFile> sourceFiles = LoadSourceFiles(files, errorMessage);
    if (!errorMessage.empty())
    {
        return result;
    }

    result.filesScanned = sourceFiles.size();
    AnalysisContext context = {options, sourceFiles};

    std::vector<std::unique_ptr<Backend>> backends;
    if (options.backendMode == BackendMode::Builtin || options.backendMode == BackendMode::Both || options.backendMode == BackendMode::Auto)
    {
        backends.push_back(CreateBuiltinBackend());
    }

    if (options.backendMode == BackendMode::Cppcheck || options.backendMode == BackendMode::Both)
    {
        backends.push_back(CreateCppcheckBackend());
    }
    else if (options.backendMode == BackendMode::Auto)
    {
        std::string versionText;
        if (IsCppcheckAvailable(options, versionText))
        {
            backends.push_back(CreateCppcheckBackend());
            if (!versionText.empty())
            {
                result.notices.push_back("cppcheck backend enabled: " + versionText);
            }
        }
        else
        {
            result.notices.push_back("cppcheck backend not available; continuing with builtin rules only");
        }
    }

    for (std::vector<std::unique_ptr<Backend>>::const_iterator backendIt = backends.begin(); backendIt != backends.end(); ++backendIt)
    {
        std::string backendError;
        if (!(*backendIt)->Analyze(context, result.issues, backendError))
        {
            if (options.backendMode == BackendMode::Auto && std::string((*backendIt)->Name()) == "cppcheck")
            {
                result.notices.push_back("cppcheck backend failed and was skipped: " + backendError);
                continue;
            }

            errorMessage = backendError;
            return result;
        }
    }

    result.issues.erase(
        std::remove_if(result.issues.begin(), result.issues.end(), [](const Issue& issue) { return issue.filePath.empty(); }),
        result.issues.end());

    std::vector<Issue> filteredIssues;
    filteredIssues.reserve(result.issues.size());

    for (std::vector<Issue>::const_iterator issueIt = result.issues.begin(); issueIt != result.issues.end(); ++issueIt)
    {
        const SourceFile* sourceFile = FindSourceFile(sourceFiles, issueIt->filePath);
        if (sourceFile == NULL)
        {
            filteredIssues.push_back(*issueIt);
            continue;
        }

        if (!IsSuppressed(*sourceFile, *issueIt))
        {
            filteredIssues.push_back(*issueIt);
        }
    }

    result.issues.swap(filteredIssues);
    std::sort(result.issues.begin(), result.issues.end(), [](const Issue& left, const Issue& right) {
        if (left.filePath != right.filePath)
        {
            return left.filePath < right.filePath;
        }
        if (left.line != right.line)
        {
            return left.line < right.line;
        }
        if (left.column != right.column)
        {
            return left.column < right.column;
        }
        if (left.engine != right.engine)
        {
            return left.engine < right.engine;
        }
        return left.ruleId < right.ruleId;
    });

    return result;
}
}