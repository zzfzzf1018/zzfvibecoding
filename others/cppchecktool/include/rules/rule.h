#pragma once

#include <memory>
#include <vector>

#include "core/issue.h"
#include "core/source_file.h"

namespace cppchecktool
{
class Rule
{
public:
    virtual ~Rule() = default;

    virtual const char* Id() const = 0;
    virtual void Analyze(const SourceFile& sourceFile, std::vector<Issue>& issues) const = 0;
};

std::vector<std::unique_ptr<Rule>> CreateBuiltinRules();
}