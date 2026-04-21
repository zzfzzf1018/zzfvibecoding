#pragma once

#include <afxwin.h>

class BizService
{
public:
    CString QuerySlowReport(int request_id) const;
};