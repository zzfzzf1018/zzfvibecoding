#include "async_job.h"

#include <chrono>
#include <exception>
#include <iostream>
#include <thread>

int main() {
    AsyncJob<int> job([](const CancellationToken& token) {
        for (int i = 0; i < 10; ++i) {
            if (token.is_cancelled()) {
                return -1;
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
        }
        return 42;
    });

    job.set_callback([](const int& result) {
        std::cout << "callback result: " << result << std::endl;
    });

    job.set_error_callback([](std::exception_ptr exception) {
        try {
            if (exception) {
                std::rethrow_exception(exception);
            }
        } catch (const std::exception& ex) {
            std::cout << "error: " << ex.what() << std::endl;
        }
    });

    job.use_thread_pool(true);

    job.start();
    std::shared_future<int> shared_future = job.get_future();

    std::cout << "main thread is working..." << std::endl;
    std::optional<int> result = job.get_for(std::chrono::milliseconds(300), TimeoutPolicy::KeepRunning);
    if (!result) {
        std::cout << "timeout, keep running" << std::endl;
    } else {
        std::cout << "job finished with result: " << *result << std::endl;
    }

    std::cout << "shared future result: " << shared_future.get() << std::endl;
    std::cout << "final status: " << static_cast<int>(job.status()) << std::endl;
    return 0;
}
