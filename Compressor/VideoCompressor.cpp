/**
 * VideoCompressor.cpp
 * CLI backend — wraps FFmpeg for video compression.
 * Communicates with VideoCompressorUI via stdout lines:
 *   DURATION:<ms>
 *   PROGRESS:<0-100>
 *   SIZE_IN:<bytes>
 *   SIZE_OUT:<bytes>
 *   STATUS: STARTING | DONE | ERROR
 *
 * Usage:
 *   VideoCompressor.exe <input> [--crf N] [--preset P] [--res WxH] [--audio kbps] [--output path]
 */

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <iostream>
#include <string>
#include <vector>
#include <filesystem>
#include <sstream>
#include <functional>
#include <algorithm>
#include <thread>

namespace fs = std::filesystem;

// ---------------------------------------------------------------------------
// Options
// ---------------------------------------------------------------------------
struct CompressOptions {
    std::wstring inputFile;
    std::wstring outputFile;
    int          crf        = 28;
    std::wstring preset     = L"medium";
    std::wstring resolution = L"";   // empty = keep source
    int          audioKbps  = 128;
};

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
static std::wstring GetOutputPath(const std::wstring& input)
{
    fs::path p(input);
    return (p.parent_path() / (p.stem().wstring() + L"_compressed.mp4")).wstring();
}

static std::wstring FindFFmpeg()
{
    wchar_t exePath[MAX_PATH] = {};
    GetModuleFileNameW(nullptr, exePath, MAX_PATH);
    fs::path exeDir = fs::path(exePath).parent_path();

    const std::vector<fs::path> candidates = {
        exeDir / L"ffmpeg.exe",
        exeDir / L"ffmpeg" / L"ffmpeg.exe",
        L"C:\\ffmpeg\\bin\\ffmpeg.exe",
        L"C:\\Program Files\\ffmpeg\\bin\\ffmpeg.exe",
    };
    for (auto& c : candidates)
        if (fs::exists(c)) return c.wstring();
    return L"ffmpeg"; // fall back to PATH
}

// Parse "HH:MM:SS.cs" → milliseconds
static int64_t ParseTimestamp(const std::string& ts)
{
    int h = 0, m = 0, s = 0, cs = 0;
    if (sscanf_s(ts.c_str(), "%d:%d:%d.%d", &h, &m, &s, &cs) >= 3)
        return (int64_t)h * 3600000 + m * 60000 + s * 1000 + cs * 10;
    return -1;
}

// ---------------------------------------------------------------------------
// Run FFmpeg via CreateProcess, read stdout+stderr from a merged pipe.
// Emits PROGRESS:N lines to our own stdout for the WPF host to consume.
// ---------------------------------------------------------------------------
static bool RunFFmpeg(const CompressOptions& opts)
{
    std::wstring ffmpeg = FindFFmpeg();

    // Build command line
    std::wostringstream cmd;
    cmd << L"\"" << ffmpeg << L"\"";
    cmd << L" -i \"" << opts.inputFile << L"\"";
    cmd << L" -c:v libx264";
    cmd << L" -crf " << opts.crf;
    cmd << L" -preset " << opts.preset;
    if (!opts.resolution.empty())
        cmd << L" -vf scale=" << opts.resolution;
    cmd << L" -c:a aac";
    cmd << L" -b:a " << opts.audioKbps << L"k";
    cmd << L" -movflags +faststart";
    cmd << L" -progress pipe:2";   // progress KV pairs on stderr
    cmd << L" -nostats";
    cmd << L" -y";
    cmd << L" \"" << opts.outputFile << L"\"";

    std::wstring cmdStr = cmd.str();

    // Create pipe for stderr (progress + regular log)
    SECURITY_ATTRIBUTES sa{ sizeof(sa), nullptr, TRUE };
    HANDLE hReadErr, hWriteErr;
    if (!CreatePipe(&hReadErr, &hWriteErr, &sa, 0)) return false;
    SetHandleInformation(hReadErr, HANDLE_FLAG_INHERIT, 0);

    // Create pipe for stdout (we don't need ffmpeg stdout but must redirect it)
    HANDLE hReadOut, hWriteOut;
    if (!CreatePipe(&hReadOut, &hWriteOut, &sa, 0)) { CloseHandle(hReadErr); CloseHandle(hWriteErr); return false; }
    SetHandleInformation(hReadOut, HANDLE_FLAG_INHERIT, 0);

    STARTUPINFOW si{ sizeof(si) };
    si.hStdOutput  = hWriteOut;
    si.hStdError   = hWriteErr;
    si.hStdInput   = GetStdHandle(STD_INPUT_HANDLE);
    si.dwFlags     = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;

    PROCESS_INFORMATION pi{};
    std::vector<wchar_t> buf(cmdStr.begin(), cmdStr.end());
    buf.push_back(0);

    if (!CreateProcessW(nullptr, buf.data(), nullptr, nullptr, TRUE,
                        CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi))
    {
        CloseHandle(hReadErr); CloseHandle(hWriteErr);
        CloseHandle(hReadOut); CloseHandle(hWriteOut);
        std::wcout << L"STATUS: ERROR\n"; std::wcout.flush();
        return false;
    }
    CloseHandle(hWriteErr);
    CloseHandle(hWriteOut);

    // Drain stdout (discard)
    std::thread drainOut([&]() {
        char tmp[512]; DWORD n;
        while (ReadFile(hReadOut, tmp, sizeof(tmp), &n, nullptr) && n) {}
    });

    // Read stderr for progress
    char    rbuf[4096];
    DWORD   bytesRead = 0;
    std::string leftover;
    int64_t totalDurationMs = -1;
    int     lastPct         = -1;

    while (ReadFile(hReadErr, rbuf, sizeof(rbuf) - 1, &bytesRead, nullptr) && bytesRead)
    {
        rbuf[bytesRead] = '\0';
        leftover += rbuf;

        size_t pos;
        while ((pos = leftover.find('\n')) != std::string::npos)
        {
            std::string line = leftover.substr(0, pos);
            leftover.erase(0, pos + 1);
            if (!line.empty() && line.back() == '\r') line.pop_back();

            // Duration from ffmpeg log line: "  Duration: HH:MM:SS.cs"
            if (totalDurationMs < 0)
            {
                auto d = line.find("Duration: ");
                if (d != std::string::npos && line.find("N/A") == std::string::npos)
                {
                    int64_t ms = ParseTimestamp(line.substr(d + 10, 11));
                    if (ms > 0)
                    {
                        totalDurationMs = ms;
                        std::cout << "DURATION:" << ms << "\n";
                        std::cout.flush();
                    }
                }
            }

            // Progress KV from -progress pipe:2
            if (line.rfind("out_time_ms=", 0) == 0)
            {
                int64_t curUs = 0;
                try { curUs = std::stoll(line.substr(12)); } catch (...) {}
                if (totalDurationMs > 0 && curUs > 0)
                {
                    int pct = (int)std::min((int64_t)99, curUs / 1000 * 100 / totalDurationMs);
                    if (pct != lastPct)
                    {
                        lastPct = pct;
                        std::cout << "PROGRESS:" << pct << "\n";
                        std::cout.flush();
                    }
                }
            }
        }
    }

    drainOut.join();
    WaitForSingleObject(pi.hProcess, INFINITE);

    DWORD exitCode = 1;
    GetExitCodeProcess(pi.hProcess, &exitCode);
    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);
    CloseHandle(hReadErr);
    CloseHandle(hReadOut);

    return exitCode == 0;
}

// ---------------------------------------------------------------------------
// main
// ---------------------------------------------------------------------------
int wmain(int argc, wchar_t* argv[])
{
    SetConsoleOutputCP(CP_UTF8);

    if (argc < 2 || std::wstring(argv[1]) == L"--help")
    {
        std::wcout
            << L"VideoCompressor.exe <input> [options]\n"
            << L"  --crf <18-40>     Quality (default 28)\n"
            << L"  --preset <name>   ultrafast/fast/medium/slow/veryslow (default medium)\n"
            << L"  --res <WxH>       Resize, e.g. 1280x720 (default: keep original)\n"
            << L"  --audio <kbps>    Audio bitrate (default 128)\n"
            << L"  --output <path>   Output file (default: <input>_compressed.mp4)\n";
        return 0;
    }

    CompressOptions opts;
    opts.inputFile = argv[1];

    if (!fs::exists(opts.inputFile))
    {
        std::wcout << L"STATUS: ERROR\n"; std::wcout.flush();
        std::wcerr << L"ERROR: file not found: " << opts.inputFile << L"\n";
        return 1;
    }

    opts.outputFile = GetOutputPath(opts.inputFile);

    for (int i = 2; i < argc; ++i)
    {
        std::wstring a = argv[i];
        if      (a == L"--crf"    && i+1 < argc) opts.crf        = std::stoi(argv[++i]);
        else if (a == L"--preset" && i+1 < argc) opts.preset     = argv[++i];
        else if (a == L"--res"    && i+1 < argc) opts.resolution = argv[++i];
        else if (a == L"--audio"  && i+1 < argc) opts.audioKbps  = std::stoi(argv[++i]);
        else if (a == L"--output" && i+1 < argc) opts.outputFile = argv[++i];
    }

    // Announce start
    std::wcout << L"STATUS: STARTING\n";
    std::wcout << L"OUTPUT:" << opts.outputFile << L"\n";
    std::wcout.flush();

    bool ok = RunFFmpeg(opts);

    if (ok)
    {
        try {
            auto inSz  = (int64_t)fs::file_size(opts.inputFile);
            auto outSz = (int64_t)fs::file_size(opts.outputFile);
            std::cout << "PROGRESS:100\n";
            std::cout << "SIZE_IN:"  << inSz  << "\n";
            std::cout << "SIZE_OUT:" << outSz << "\n";
        } catch (...) {}
        std::wcout << L"STATUS: DONE\n";
        std::wcout.flush();
        return 0;
    }
    else
    {
        std::wcout << L"STATUS: ERROR\n";
        std::wcout.flush();
        return 1;
    }
}
