#pragma once

struct BusinessTaskFinishedEvent {
    int task_id = 0;
    DWORD worker_thread_id = 0;
    DWORD ui_thread_id = 0;
    CString result_text;
};