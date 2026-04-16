#pragma once

#include <iostream>
#include <string>

class ILogger
{
public:
    virtual ~ILogger()
    {
    }

    virtual void Log(const std::string& message) = 0;
};

class ConsoleLogger : public ILogger
{
public:
    virtual void Log(const std::string& message) override
    {
        std::cout << message << std::endl;
    }
};