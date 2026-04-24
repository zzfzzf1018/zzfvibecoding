#pragma once

#include <string>

#include "core/driver.h"
#include "core/scan_options.h"

namespace cppchecktool
{
bool WriteReport(const ScanResult& result, const ScanOptions& options, std::string& errorMessage);
}