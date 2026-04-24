#include <memory>
#include <string>

std::unique_ptr<int> CreateValue()
{
    return std::make_unique<int>(7);
}

int main()
{
    const std::string name = "safe";
    return static_cast<int>(name.size());
}