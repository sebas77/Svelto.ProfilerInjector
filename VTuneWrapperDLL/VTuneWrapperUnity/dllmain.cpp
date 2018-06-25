// dllmain.cpp : Defines the entry point for the DLL application.
#include "stdafx.h"
#include "include\ittnotify.h"

extern "C" __declspec(dllexport) int __stdcall  VTuneCreateEventEx(LPCWSTR name)
{
	return __itt_event_create(name, lstrlen(name));
}

extern "C" __declspec(dllexport) void __stdcall  VTuneBeginEventEx(int handle)
{
	__itt_event_start(handle);
}
extern "C" __declspec(dllexport) void __stdcall  VTuneEndEventEx(int handle)
{
	__itt_event_end(handle);
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

