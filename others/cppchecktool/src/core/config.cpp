#include "core/config.h"

#include <algorithm>
#include <cctype>
#include <exception>

#include "core/filesystem_compat.h"

#include <yaml-cpp/yaml.h>

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

std::string StripQuotes(const std::string& value)
{
    if (value.size() >= 2)
    {
        const char first = value.front();
        const char last = value.back();
        if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
        {
            return value.substr(1, value.size() - 2);
        }
    }

    return value;
}

std::string ResolveRelativeToConfig(const fs::path& configFilePath, const std::string& value)
{
    if (value.empty())
    {
        return value;
    }

    fs::path resolved(value);
    if (resolved.is_relative())
    {
        resolved = configFilePath.parent_path() / resolved;
    }

    return resolved.lexically_normal().generic_string();
}

bool ReadStringSequence(const YAML::Node& node, std::vector<std::string>& output, std::string& errorMessage, const char* key)
{
    if (!node)
    {
        return true;
    }

    if (!node.IsSequence())
    {
        errorMessage = std::string("config key must be a YAML sequence: ") + key;
        return false;
    }

    for (std::size_t index = 0; index < node.size(); ++index)
    {
        if (!node[index].IsScalar())
        {
            errorMessage = std::string("config sequence item must be scalar: ") + key;
            return false;
        }
        output.push_back(node[index].as<std::string>());
    }
    return true;
}

template <typename Parser>
bool ReadScalarIfPresent(const YAML::Node& root, const char* key, Parser parser, std::string& errorMessage)
{
    const YAML::Node node = root[key];
    if (!node)
    {
        return true;
    }

    if (!node.IsScalar())
    {
        errorMessage = std::string("config key must be scalar: ") + key;
        return false;
    }

    return parser(node.as<std::string>());
}
}

bool ParseOutputFormat(const std::string& value, OutputFormat& format)
{
    const std::string lowered = ToLowerCopy(value);
    if (lowered == "text")
    {
        format = OutputFormat::Text;
        return true;
    }
    if (lowered == "sarif")
    {
        format = OutputFormat::Sarif;
        return true;
    }
    return false;
}

bool ParseBackendMode(const std::string& value, BackendMode& mode)
{
    const std::string lowered = ToLowerCopy(value);
    if (lowered == "builtin")
    {
        mode = BackendMode::Builtin;
        return true;
    }
    if (lowered == "cppcheck")
    {
        mode = BackendMode::Cppcheck;
        return true;
    }
    if (lowered == "both")
    {
        mode = BackendMode::Both;
        return true;
    }
    if (lowered == "auto")
    {
        mode = BackendMode::Auto;
        return true;
    }
    return false;
}

bool LoadConfigFile(const std::string& filePath, ScanOptions& options, std::string& errorMessage)
{
    try
    {
        const YAML::Node root = YAML::LoadFile(filePath);
        if (!root || !root.IsMap())
        {
            errorMessage = "config root must be a YAML mapping";
            return false;
        }

        if (!ReadScalarIfPresent(root, "target", [&](const std::string& value) {
                options.targetPath = value;
                return true;
            }, errorMessage))
        {
            return false;
        }

        if (!ReadScalarIfPresent(root, "output", [&](const std::string& value) {
                options.outputPath = value;
                return true;
            }, errorMessage))
        {
            return false;
        }

        if (!ReadScalarIfPresent(root, "compile_database", [&](const std::string& value) {
                options.compileDatabasePath = value;
                return true;
            }, errorMessage))
        {
            return false;
        }

        if (!ReadScalarIfPresent(root, "cppcheck_path", [&](const std::string& value) {
                options.cppcheckPath = value;
                return true;
            }, errorMessage))
        {
            return false;
        }

        if (!ReadScalarIfPresent(root, "cppcheck_enable", [&](const std::string& value) {
                options.cppcheckEnable = value;
                return true;
            }, errorMessage))
        {
            return false;
        }

        if (!ReadScalarIfPresent(root, "format", [&](const std::string& value) {
                if (!ParseOutputFormat(value, options.outputFormat))
                {
                    errorMessage = "unsupported output format in config: " + value;
                    return false;
                }
                return true;
            }, errorMessage))
        {
            return false;
        }

        if (!ReadScalarIfPresent(root, "backend", [&](const std::string& value) {
                if (!ParseBackendMode(value, options.backendMode))
                {
                    errorMessage = "unsupported backend mode in config: " + value;
                    return false;
                }
                return true;
            }, errorMessage))
        {
            return false;
        }

        const YAML::Node recursiveNode = root["recursive"];
        if (recursiveNode)
        {
            if (!recursiveNode.IsScalar())
            {
                errorMessage = "config key must be scalar: recursive";
                return false;
            }
            options.recursive = recursiveNode.as<bool>();
        }

        if (!ReadStringSequence(root["exclude"], options.excludedPaths, errorMessage, "exclude"))
        {
            return false;
        }

        if (!ReadStringSequence(root["extensions"], options.extensions, errorMessage, "extensions"))
        {
            return false;
        }
    }
    catch (const YAML::Exception& ex)
    {
        errorMessage = std::string("failed to parse YAML config: ") + ex.what();
        return false;
    }
    catch (const std::exception& ex)
    {
        errorMessage = std::string("failed to load config: ") + ex.what();
        return false;
    }

    const fs::path configFilePath(filePath);
    options.targetPath = ResolveRelativeToConfig(configFilePath, options.targetPath);
    options.outputPath = ResolveRelativeToConfig(configFilePath, options.outputPath);
    options.compileDatabasePath = ResolveRelativeToConfig(configFilePath, options.compileDatabasePath);
    if (options.cppcheckPath.find('/') != std::string::npos || options.cppcheckPath.find('\\') != std::string::npos)
    {
        options.cppcheckPath = ResolveRelativeToConfig(configFilePath, options.cppcheckPath);
    }

    return true;
}
}