#include "backend/backend.h"

#include <memory>

#include "rules/rule.h"

namespace cppchecktool
{
namespace
{
class BuiltinBackend : public Backend
{
public:
    const char* Name() const override
    {
        return "builtin";
    }

    bool Analyze(const AnalysisContext& context, std::vector<Issue>& issues, std::string& errorMessage) const override
    {
        (void)errorMessage;

        std::vector<std::unique_ptr<Rule>> rules = CreateBuiltinRules();
        for (std::vector<SourceFile>::const_iterator sourceIt = context.sourceFiles.begin(); sourceIt != context.sourceFiles.end(); ++sourceIt)
        {
            for (std::vector<std::unique_ptr<Rule>>::const_iterator ruleIt = rules.begin(); ruleIt != rules.end(); ++ruleIt)
            {
                const std::size_t beforeCount = issues.size();
                (*ruleIt)->Analyze(*sourceIt, issues);
                for (std::size_t index = beforeCount; index < issues.size(); ++index)
                {
                    issues[index].engine = Name();
                }
            }
        }

        return true;
    }
};
}

std::unique_ptr<Backend> CreateBuiltinBackend()
{
    return std::unique_ptr<Backend>(new BuiltinBackend());
}
}