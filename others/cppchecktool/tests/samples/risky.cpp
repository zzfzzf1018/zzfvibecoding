#define _CRT_SECURE_NO_WARNINGS

#include <cstdio>
#include <cstdlib>
#include <thread>

using namespace std;

void demo()
{
    char buffer[8] = {};
    strcpy(buffer, "0123456789");

    int* value = new int(42);
    delete value;

    std::thread worker([]() {});
    worker.detach();

    try
    {
        throw 1;
    }
    catch (...)
    {
    }

    // check-ignore: SEC001
    system("pause");
}