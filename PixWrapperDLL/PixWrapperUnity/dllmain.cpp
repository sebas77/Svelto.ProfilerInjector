//it works only in debug and with the define USE_PIX

// dllmain.cpp : Defines the entry point for the DLL application.
#include "stdafx.h"
#include "pix3.h"

extern "C" __declspec(dllexport) void __stdcall  PIXBeginEventEx(UINT64 color, PCSTR string)
{
    PIXBeginEvent(color, "%s", string);
}
extern "C" __declspec(dllexport) void __stdcall  PIXEndEventEx()
{
    PIXEndEvent();
}

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
					 )
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
	case DLL_PROCESS_DETACH:
		break;
	}
	return TRUE;
}

