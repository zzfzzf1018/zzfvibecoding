#pragma once

#include <afxwin.h>
#include <afxdisp.h>

class CMfcAsyncAwaitApp : public CWinApp
{
public:
    virtual BOOL InitInstance();
};

extern CMfcAsyncAwaitApp theApp;