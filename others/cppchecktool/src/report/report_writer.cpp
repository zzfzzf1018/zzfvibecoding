#include "report/report_writer.h"

#include <fstream>
#include <iostream>
#include <sstream>

namespace cppchecktool
{
namespace
{
std::string JsonEscape(const std::string& value)
{
    std::ostringstream stream;
    for (std::string::const_iterator it = value.begin(); it != value.end(); ++it)
    {
        switch (*it)
        {
        case '\\':
            stream << "\\\\";
            break;
        case '"':
            stream << "\\\"";
            break;
        case '\n':
            stream << "\\n";
            break;
        case '\r':
            stream << "\\r";
            break;
        case '\t':
            stream << "\\t";
            break;
        default:
            stream << *it;
            break;
        }
    }

    return stream.str();
}

std::string BuildTextReport(const ScanResult& result)
{
    std::ostringstream stream;
    stream << "cppchecktool report\n";
    stream << "files scanned: " << result.filesScanned << "\n";
    stream << "issues: " << result.issues.size() << "\n\n";

    for (std::vector<std::string>::const_iterator it = result.notices.begin(); it != result.notices.end(); ++it)
    {
        stream << "notice: " << *it << '\n';
    }
    if (!result.notices.empty())
    {
        stream << '\n';
    }

    for (std::vector<Issue>::const_iterator it = result.issues.begin(); it != result.issues.end(); ++it)
    {
        stream << it->filePath << ':' << it->line << ':' << it->column << ": [" << it->ruleId << "]";
        if (!it->engine.empty())
        {
            stream << " [" << it->engine << "]";
        }
        stream << ' ' << ToString(it->severity)
               << " (" << ToString(it->category) << ") " << it->message;
        if (!it->suggestion.empty())
        {
            stream << " | suggestion: " << it->suggestion;
        }
        stream << '\n';
    }

    return stream.str();
}

std::string BuildSarifReport(const ScanResult& result)
{
    std::ostringstream stream;
    stream << "{\n";
    stream << "  \"$schema\": \"https://json.schemastore.org/sarif-2.1.0.json\",\n";
    stream << "  \"version\": \"2.1.0\",\n";
    stream << "  \"runs\": [\n";
    stream << "    {\n";
    stream << "      \"tool\": {\n";
    stream << "        \"driver\": {\n";
    stream << "          \"name\": \"cppchecktool\",\n";
    stream << "          \"informationUri\": \"https://example.local/cppchecktool\",\n";
    stream << "          \"rules\": [\n";

    for (std::size_t index = 0; index < result.issues.size(); ++index)
    {
        const Issue& issue = result.issues[index];
        stream << "            {\n";
        stream << "              \"id\": \"" << JsonEscape(issue.ruleId) << "\",\n";
        stream << "              \"shortDescription\": { \"text\": \"" << JsonEscape(issue.message) << "\" },\n";
        stream << "              \"properties\": { \"category\": \"" << JsonEscape(ToString(issue.category)) << "\" }\n";
        stream << "            }";
        if (index + 1 != result.issues.size())
        {
            stream << ',';
        }
        stream << "\n";
    }

    stream << "          ]\n";
    stream << "        }\n";
    stream << "      },\n";
    stream << "      \"results\": [\n";

    for (std::size_t index = 0; index < result.issues.size(); ++index)
    {
        const Issue& issue = result.issues[index];
        stream << "        {\n";
        stream << "          \"ruleId\": \"" << JsonEscape(issue.ruleId) << "\",\n";
        stream << "          \"level\": \"" << JsonEscape(ToString(issue.severity)) << "\",\n";
        stream << "          \"message\": { \"text\": \"" << JsonEscape(issue.message) << "\" },\n";
        stream << "          \"locations\": [\n";
        stream << "            {\n";
        stream << "              \"physicalLocation\": {\n";
        stream << "                \"artifactLocation\": { \"uri\": \"" << JsonEscape(issue.filePath) << "\" },\n";
        stream << "                \"region\": { \"startLine\": " << issue.line << ", \"startColumn\": " << issue.column << " }\n";
        stream << "              }\n";
        stream << "            }\n";
        stream << "          ]\n";
        stream << "        }";
        if (index + 1 != result.issues.size())
        {
            stream << ',';
        }
        stream << "\n";
    }

    stream << "      ]\n";
    stream << "    }\n";
    stream << "  ]\n";
    stream << "}\n";
    return stream.str();
}

bool WriteToDestination(const std::string& content, const std::string& outputPath, std::string& errorMessage)
{
    if (outputPath.empty())
    {
        std::cout << content;
        return true;
    }

    std::ofstream stream(outputPath.c_str(), std::ios::out | std::ios::binary | std::ios::trunc);
    if (!stream)
    {
        errorMessage = "failed to write report: " + outputPath;
        return false;
    }

    stream << content;
    return true;
}
}

bool WriteReport(const ScanResult& result, const ScanOptions& options, std::string& errorMessage)
{
    if (options.outputFormat == OutputFormat::Sarif)
    {
        return WriteToDestination(BuildSarifReport(result), options.outputPath, errorMessage);
    }

    return WriteToDestination(BuildTextReport(result), options.outputPath, errorMessage);
}
}