#pragma once

#include <string>

#include "core/scan_options.h"

namespace cppchecktool
{
bool ParseOutputFormat(const std::string& value, OutputFormat& format);
bool ParseBackendMode(const std::string& value, BackendMode& mode);
bool LoadConfigFile(const std::string& filePath, ScanOptions& options, std::string& errorMessage);
}