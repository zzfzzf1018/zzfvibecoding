#include "rules/rule.h"

#include <algorithm>
#include <cctype>
#include <regex>
#include <string>

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

std::size_t ColumnFromMatch(const std::smatch& match)
{
    return static_cast<std::size_t>(match.position()) + 1;
}

std::size_t LineFromOffset(const std::string& content, const std::size_t offset)
{
    std::size_t line = 1;
    for (std::size_t index = 0; index < offset && index < content.size(); ++index)
    {
        if (content[index] == '\n')
        {
            ++line;
        }
    }
    return line;
}

class UnsafeFunctionRule : public Rule
{
public:
    const char* Id() const override
    {
        return "SEC001";
    }

    void Analyze(const SourceFile& sourceFile, std::vector<Issue>& issues) const override
    {
        const std::regex pattern("\\b(strcpy|sprintf|vsprintf|gets|system)\\s*\\(", std::regex::icase);
        for (std::size_t index = 0; index < sourceFile.lines.size(); ++index)
        {
            std::smatch match;
            if (std::regex_search(sourceFile.lines[index], match, pattern))
            {
                Issue issue;
                issue.ruleId = Id();
                issue.severity = Severity::Error;
                issue.category = Category::Security;
                issue.message = "dangerous C/C++ runtime API detected";
                issue.suggestion = "prefer bounded or safer alternatives and validate input size";
                issue.filePath = sourceFile.path;
                issue.line = index + 1;
                issue.column = ColumnFromMatch(match);
                issues.push_back(issue);
            }
        }
    }
};

class RawNewRule : public Rule
{
public:
    const char* Id() const override
    {
        return "MEM001";
    }

    void Analyze(const SourceFile& sourceFile, std::vector<Issue>& issues) const override
    {
        const std::regex pattern("(^|[^a-zA-Z0-9_])new\\s+(?!\\()", std::regex::icase);
        for (std::size_t index = 0; index < sourceFile.lines.size(); ++index)
        {
            std::smatch match;
            if (std::regex_search(sourceFile.lines[index], match, pattern))
            {
                Issue issue;
                issue.ruleId = Id();
                issue.severity = Severity::Warning;
                issue.category = Category::Memory;
                issue.message = "raw new detected; ownership may be unclear";
                issue.suggestion = "prefer std::make_unique, std::make_shared, or stack allocation";
                issue.filePath = sourceFile.path;
                issue.line = index + 1;
                issue.column = ColumnFromMatch(match) + 1;
                issues.push_back(issue);
            }
        }
    }
};

class RawDeleteRule : public Rule
{
public:
    const char* Id() const override
    {
        return "MEM002";
    }

    void Analyze(const SourceFile& sourceFile, std::vector<Issue>& issues) const override
    {
        const std::regex pattern("\\bdelete\\s+", std::regex::icase);
        for (std::size_t index = 0; index < sourceFile.lines.size(); ++index)
        {
            std::smatch match;
            if (std::regex_search(sourceFile.lines[index], match, pattern))
            {
                Issue issue;
                issue.ruleId = Id();
                issue.severity = Severity::Warning;
                issue.category = Category::Memory;
                issue.message = "raw delete detected; manual lifetime management is error-prone";
                issue.suggestion = "prefer RAII wrappers and standard smart pointers";
                issue.filePath = sourceFile.path;
                issue.line = index + 1;
                issue.column = ColumnFromMatch(match) + 1;
                issues.push_back(issue);
            }
        }
    }
};

class DetachedThreadRule : public Rule
{
public:
    const char* Id() const override
    {
        return "CON001";
    }

    void Analyze(const SourceFile& sourceFile, std::vector<Issue>& issues) const override
    {
        const std::regex pattern("\\.detach\\s*\\(", std::regex::icase);
        for (std::size_t index = 0; index < sourceFile.lines.size(); ++index)
        {
            std::smatch match;
            if (std::regex_search(sourceFile.lines[index], match, pattern))
            {
                Issue issue;
                issue.ruleId = Id();
                issue.severity = Severity::Warning;
                issue.category = Category::Concurrency;
                issue.message = "detached thread detected; shutdown and lifetime coordination may be unsafe";
                issue.suggestion = "prefer joinable thread ownership or a task scheduler";
                issue.filePath = sourceFile.path;
                issue.line = index + 1;
                issue.column = ColumnFromMatch(match) + 1;
                issues.push_back(issue);
            }
        }
    }
};

class UsingNamespaceStdRule : public Rule
{
public:
    const char* Id() const override
    {
        return "MNT001";
    }

    void Analyze(const SourceFile& sourceFile, std::vector<Issue>& issues) const override
    {
        const std::regex pattern("\\busing\\s+namespace\\s+std\\s*;", std::regex::icase);
        for (std::size_t index = 0; index < sourceFile.lines.size(); ++index)
        {
            std::smatch match;
            if (std::regex_search(sourceFile.lines[index], match, pattern))
            {
                Issue issue;
                issue.ruleId = Id();
                issue.severity = Severity::Note;
                issue.category = Category::Maintainability;
                issue.message = "using namespace std may increase symbol collisions";
                issue.suggestion = "prefer explicit std:: qualification in headers and shared code";
                issue.filePath = sourceFile.path;
                issue.line = index + 1;
                issue.column = ColumnFromMatch(match) + 1;
                issues.push_back(issue);
            }
        }
    }
};

class CRunTimeMacroRule : public Rule
{
public:
    const char* Id() const override
    {
        return "MSC001";
    }

    void Analyze(const SourceFile& sourceFile, std::vector<Issue>& issues) const override
    {
        const std::regex pattern("#\\s*define\\s+_CRT_SECURE_NO_WARNINGS\\b", std::regex::icase);
        for (std::size_t index = 0; index < sourceFile.lines.size(); ++index)
        {
            std::smatch match;
            if (std::regex_search(sourceFile.lines[index], match, pattern))
            {
                Issue issue;
                issue.ruleId = Id();
                issue.severity = Severity::Warning;
                issue.category = Category::Platform;
                issue.message = "_CRT_SECURE_NO_WARNINGS disables useful MSVC security diagnostics";
                issue.suggestion = "remove the macro and migrate to safer CRT alternatives where practical";
                issue.filePath = sourceFile.path;
                issue.line = index + 1;
                issue.column = ColumnFromMatch(match) + 1;
                issues.push_back(issue);
            }
        }
    }
};

class EmptyCatchRule : public Rule
{
public:
    const char* Id() const override
    {
        return "REL001";
    }

    void Analyze(const SourceFile& sourceFile, std::vector<Issue>& issues) const override
    {
        const std::regex pattern("catch\\s*\\([^\\)]*\\)\\s*\\{\\s*\\}", std::regex::icase);
        std::sregex_iterator it(sourceFile.content.begin(), sourceFile.content.end(), pattern);
        const std::sregex_iterator end;
        for (; it != end; ++it)
        {
            Issue issue;
            issue.ruleId = Id();
            issue.severity = Severity::Warning;
            issue.category = Category::Reliability;
            issue.message = "empty catch block detected; failures may be silently ignored";
            issue.suggestion = "log, translate, or intentionally document exception suppression";
            issue.filePath = sourceFile.path;
            issue.line = LineFromOffset(sourceFile.content, static_cast<std::size_t>(it->position()));
            issue.column = 1;
            issues.push_back(issue);
        }
    }
};
}

std::vector<std::unique_ptr<Rule>> CreateBuiltinRules()
{
    std::vector<std::unique_ptr<Rule>> rules;
    rules.push_back(std::unique_ptr<Rule>(new UnsafeFunctionRule()));
    rules.push_back(std::unique_ptr<Rule>(new RawNewRule()));
    rules.push_back(std::unique_ptr<Rule>(new RawDeleteRule()));
    rules.push_back(std::unique_ptr<Rule>(new DetachedThreadRule()));
    rules.push_back(std::unique_ptr<Rule>(new UsingNamespaceStdRule()));
    rules.push_back(std::unique_ptr<Rule>(new CRunTimeMacroRule()));
    rules.push_back(std::unique_ptr<Rule>(new EmptyCatchRule()));
    return rules;
}
}