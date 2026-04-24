#pragma once

#include <string>
#include <vector>

#include "core/issue.h"
#include "core/scan_options.h"

namespace cppchecktool
{
struct ScanResult
{
    std::vector<Issue> issues;
    std::vector<std::string> notices;
    std::size_t filesScanned = 0;
};

class Driver
{
public:
    ScanResult Scan(const ScanOptions& options, std::string& errorMessage) const;
};
}