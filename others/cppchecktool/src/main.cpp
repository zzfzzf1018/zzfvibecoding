#include <iostream>
#include <string>
#include <vector>

#include "core/config.h"
#include "core/driver.h"
#include "report/report_writer.h"

namespace
{
void PrintUsage()
{
    std::cout << "cppchecktool - lightweight static analysis for C/C++ code\n";
    std::cout << "usage: cppchecktool [options] <path>\n\n";
    std::cout << "options:\n";
    std::cout << "  --config <file>        load options from a YAML config file\n";
    std::cout << "  --compile-db <file>    read files from compile_commands.json\n";
    std::cout << "  --backend <mode>       builtin|cppcheck|both|auto (default: auto)\n";
    std::cout << "  --cppcheck-path <exe>  path to cppcheck executable\n";
    std::cout << "  --format text|sarif    report format (default: text)\n";
    std::cout << "  --output <file>        write the report to a file\n";
    std::cout << "  --exclude <pattern>    skip files or directories containing the pattern\n";
    std::cout << "  --non-recursive        scan only the top-level directory\n";
    std::cout << "  --help                 show this help\n";
}
}

int main(int argc, char** argv)
{
    cppchecktool::ScanOptions options;
    std::string configPath;
    std::vector<std::string> positionalArguments;

    for (int index = 1; index < argc; ++index)
    {
        const std::string argument = argv[index];
        if (argument == "--config" && index + 1 < argc)
        {
            configPath = argv[++index];
        }
    }

    std::string errorMessage;
    if (!configPath.empty())
    {
        if (!cppchecktool::LoadConfigFile(configPath, options, errorMessage))
        {
            std::cerr << errorMessage << '\n';
            return 2;
        }
        options.configPath = configPath;
    }

    for (int index = 1; index < argc; ++index)
    {
        const std::string argument = argv[index];
        if (argument == "--help" || argument == "-h")
        {
            PrintUsage();
            return 0;
        }

        if (argument == "--config" && index + 1 < argc)
        {
            ++index;
            continue;
        }

        if (argument == "--compile-db" && index + 1 < argc)
        {
            options.compileDatabasePath = argv[++index];
            continue;
        }

        if (argument == "--backend" && index + 1 < argc)
        {
            if (!cppchecktool::ParseBackendMode(argv[++index], options.backendMode))
            {
                std::cerr << "unsupported backend mode\n";
                return 2;
            }
            continue;
        }

        if (argument == "--cppcheck-path" && index + 1 < argc)
        {
            options.cppcheckPath = argv[++index];
            continue;
        }

        if (argument == "--format" && index + 1 < argc)
        {
            if (!cppchecktool::ParseOutputFormat(argv[++index], options.outputFormat))
            {
                std::cerr << "unsupported format\n";
                return 2;
            }
            continue;
        }

        if (argument == "--output" && index + 1 < argc)
        {
            options.outputPath = argv[++index];
            continue;
        }

        if (argument == "--exclude" && index + 1 < argc)
        {
            options.excludedPaths.push_back(argv[++index]);
            continue;
        }

        if (argument == "--non-recursive")
        {
            options.recursive = false;
            continue;
        }

        positionalArguments.push_back(argument);
    }

    if (!positionalArguments.empty())
    {
        options.targetPath = positionalArguments.front();
    }

    if (options.targetPath.empty() && options.compileDatabasePath.empty())
    {
        PrintUsage();
        return 2;
    }

    cppchecktool::Driver driver;
    const cppchecktool::ScanResult result = driver.Scan(options, errorMessage);
    if (!errorMessage.empty())
    {
        std::cerr << errorMessage << '\n';
        return 2;
    }

    if (!cppchecktool::WriteReport(result, options, errorMessage))
    {
        std::cerr << errorMessage << '\n';
        return 2;
    }

    return result.issues.empty() ? 0 : 1;
}