#pragma once

#include <memory>
#include <string>
#include <vector>

#include "core/issue.h"
#include "core/scan_options.h"
#include "core/source_file.h"

namespace cppchecktool
{
struct AnalysisContext
{
    const ScanOptions& options;
    const std::vector<SourceFile>& sourceFiles;
};

class Backend
{
public:
    virtual ~Backend() = default;

    virtual const char* Name() const = 0;
    virtual bool Analyze(const AnalysisContext& context, std::vector<Issue>& issues, std::string& errorMessage) const = 0;
};

std::unique_ptr<Backend> CreateBuiltinBackend();
std::unique_ptr<Backend> CreateCppcheckBackend();
bool IsCppcheckAvailable(const ScanOptions& options, std::string& versionText);
}