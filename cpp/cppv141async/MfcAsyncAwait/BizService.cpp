#include "BizService.h"

CString BizService::QuerySlowReport(int request_id) const
{
    ::Sleep(1800);

    SYSTEMTIME local_time = {};
    ::GetLocalTime(&local_time);

    CString report;
    report.Format(
        L"request_id = %d\r\nworker_thread_id = %lu\r\nfinished_at = %04d-%02d-%02d %02d:%02d:%02d",
        request_id,
        ::GetCurrentThreadId(),
        local_time.wYear,
        local_time.wMonth,
        local_time.wDay,
        local_time.wHour,
        local_time.wMinute,
        local_time.wSecond);

    return report;
}