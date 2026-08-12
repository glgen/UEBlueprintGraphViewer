#include <BlueprintDebugger.hpp>

#include <vector>
#include <unordered_map>
#include <iostream>
#include <thread>
#include <ranges>

#include <DynamicOutput/DynamicOutput.hpp>
#include <Helpers/String.hpp>
#include <Unreal/UObjectGlobals.hpp>
#include <Unreal/CoreUObject/UObject/Class.hpp>
#include <Unreal/FString.hpp>
#include <Unreal/Core/Containers/Array.hpp>
#include <Unreal/FFrame.hpp>
#include <Unreal/Script.hpp>
#include <Unreal/ReflectedFunction.hpp>
#include <Unreal/Signatures.hpp>
#include <Unreal/CoreUObject/UObject/UnrealType.hpp>

#define IMGUI_DEFINE_MATH_OPERATORS
#include <imgui.h>
#include <imgui_internal.h>
#include <misc/cpp/imgui_stdlib.h>
#include <glaze/glaze.hpp>

#include "Profiler/Profiler.hpp"

#include <UE4SSProgram.hpp>
#include <windows.h>
#include <string>

namespace RC::GUI::KismetDebuggerMod
{
    using namespace RC::Unreal;

    FNativeFuncPtr GNativesOriginal[EExprToken::EX_Max];
    volatile bool is_hooked = false; // cannot hook *immediately* as GNatives is populated at runtime
    volatile bool is_pipe_connected = false;

    volatile bool should_pause = false;
    volatile bool should_next = false;
    volatile bool should_enable = false;
    UFunction* next_func;
    std::optional<PausedContext> context;
    std::mutex context_mutex;

    BreakpointStore g_breakpoints;

    PROCESS_INFORMATION piProcInfo;
    HANDLE hPipe = NULL;

    void hook_expr_internal(UObject* Context, FFrame& Stack, void* RESULT_DECL, EExprToken N) {
        UFunction* fn = Stack.Node();
        StringType name = Stack.Node()->GetFullName();
        
        ProfilerTransientScopeNamed(scope, to_string(name).c_str(), true);
        

        size_t index = Stack.Code() - fn->GetScript().GetData() - 1;

        if (should_next && next_func != fn)
        {
            GNativesOriginal[N](Context, Stack, RESULT_DECL);
            return;
        }
        else
        {
            should_next = false;
            next_func = NULL;
        }

        if (should_pause || g_breakpoints.has_breakpoint(name, index))
        {
            std::unique_lock<std::mutex> lock_a(context_mutex);
            should_pause = true;
            PausedContext ctx{
                .expr = N,
                .context = Context,
                .stack = &Stack,
            };
            context = std::optional<PausedContext>(ctx);
            lock_a.unlock();
            while (should_pause && !should_next)
            {
                std::this_thread::sleep_for(std::chrono::milliseconds(10));
            }
            std::unique_lock<std::mutex> lock_b(context_mutex);
            context = std::nullopt;
            lock_b.unlock();
        }

        GNativesOriginal[N](Context, Stack, RESULT_DECL);
    }
    
    template <unsigned N>
    void hook_expr(UObject* Context, FFrame& Stack, void* RESULT_DECL) {
        hook_expr_internal(Context, Stack, RESULT_DECL, static_cast<EExprToken>(N));
    }

    template <unsigned N> void hook_all() {
        GNativesOriginal[N - 1] = GNatives_Internal[N - 1];
        GNatives_Internal[N - 1] = &hook_expr<N - 1>;
        hook_all<N - 1>();
    }
    template <> void hook_all<0>() {}

    std::vector<std::wstring> split_wstring(const std::wstring& input, const std::wstring& delimiter) {
        std::vector<std::wstring> tokens;
        size_t start = 0;
        size_t end = input.find(delimiter);

        while (end != std::wstring::npos) {
            tokens.push_back(input.substr(start, end - start));
            start = end + delimiter.length();
            end = input.find(delimiter, start);
        }

        tokens.push_back(input.substr(start));

        return tokens;
    }

    typedef std::unordered_map<std::string, std::unordered_set<size_t>> JsonBreakpoints;

    BreakpointStore::BreakpointStore()
    {
    }
    BreakpointStore::~BreakpointStore()
    {
    }
    auto BreakpointStore::load(std::filesystem::path& path) -> void
    {
        JsonBreakpoints breakpoints{};
        auto ec = glz::read_file_json(breakpoints, path.string(), std::string{});

        for (const auto& [fn, bps] : breakpoints)
        {
            auto wfn = ensure_str(fn);
            for (const auto& bp : bps)
            {
                add_breakpoint(wfn, bp);
            }
        }
    }
    auto BreakpointStore::save() -> void
    {
            JsonBreakpoints breakpoints{};
            for (const auto& [fn, bps] : m_breakpoints_by_name) {
                if (bps) breakpoints[to_string(fn)] = *bps;
            }
            auto ec = glz::write_file_json(breakpoints, Debugger::m_save_path.string(), std::string{});

    }
    
    auto BreakpointStore::has_breakpoint(const StringType& fn, size_t index) -> bool
    {
        const auto it = m_breakpoints_by_name.find(fn);
        if (it != m_breakpoints_by_name.end() && it->second)
        {
            return it->second->contains(index);
        }
        return false;
    }
    auto BreakpointStore::add_breakpoint(UFunction* fn, size_t index) -> void
    {
        std::shared_ptr<FunctionBreakpoints> bps;
        auto [it_fn, inserted_fn] = m_breakpoints_by_function.emplace(fn, nullptr);
        auto [it_name, inserted_name] = m_breakpoints_by_name.emplace(fn->GetFullName(), nullptr);
        if (!inserted_fn && it_fn->second) bps = it_fn->second;
        if (!inserted_name && it_name->second) bps = it_name->second;

        if (!bps)
            bps = it_fn->second = it_name->second = std::make_shared<FunctionBreakpoints>();

        bps->emplace(index);

        save();
    }
    auto BreakpointStore::add_breakpoint(const StringType& fn, size_t index) -> void
    {
        std::shared_ptr<FunctionBreakpoints> bps;
        auto [it_name, inserted_name] = m_breakpoints_by_name.emplace(fn, nullptr);
        if (!inserted_name && it_name->second) bps = it_name->second;

        if (!bps)
            bps = it_name->second = std::make_shared<FunctionBreakpoints>();

        bps->emplace(index);

        save();
    }
    auto BreakpointStore::remove_breakpoint(const StringType& fn, size_t index) -> void
    {
        std::shared_ptr<FunctionBreakpoints> bps;
        auto [it_name, inserted_name] = m_breakpoints_by_name.emplace(fn, nullptr);
        if (!inserted_name && it_name->second) bps = it_name->second;

        if (bps)
            bps->erase(index);

        save();
    }

    Debugger::Debugger() : m_breakpoints(g_breakpoints)
    {
        m_save_path = StringType{UE4SSProgram::get_program().get_working_directory()} + fmt::format(STR("\\Mods\\BlueprintDebugger\\config\\breakpoints.json"));
        m_uebpv_path = StringType{UE4SSProgram::get_program().get_working_directory()} + fmt::format(STR("\\Mods\\BlueprintDebugger\\config\\uebpv.txt"));
    }
    Debugger::~Debugger()
    {
        if (is_hooked)
        {
            for (int i = 0; i < EExprToken::EX_Max; i++)
            {
                GNatives_Internal[i] = GNativesOriginal[i];
            }
            is_hooked = false;
            should_pause = false;
            should_next = false;

            // Give main thread some time to exit hooked function. This can
            // possibly be called to unload the DLL, in which case bad things
            // will happen if the main thread is still inside the hook when it
            // disappears.
            // TODO: Need to find a way to guarantee the main thread has exited
            // the hook before continuing.
            std::this_thread::sleep_for(std::chrono::milliseconds(500));
        }
    }

    auto Debugger::enable() -> void
    {
        // do a bunch of setup on enable rather than mod init because a lot of things aren't ready at mod init

        // hack to delay breakpoint loading because working directory changes at some point during load
        try
        {
            m_breakpoints.load(m_save_path);
        }
        catch (std::exception& e)
        {
            Output::send<LogLevel::Warning>(STR("[BlueprintDebugger]: Failed to load breakpoints: {}\n"), ensure_str(e.what()));
        }

        if (GNatives_Internal != nullptr)
        {
            // finally actually enable the debugger
            hook_all<EExprToken::EX_Max>();
            is_hooked = true;
            return;
        }
        Output::send<LogLevel::Error>(STR("[BlueprintDebugger]: GNatives not found.\n"));
    }
    auto Debugger::disable() -> void
    {
        for (int i = 0; i < EExprToken::EX_Max; i++)
        {
            GNatives_Internal[i] = GNativesOriginal[i];
        }
        is_hooked = false;
        should_pause = false;
        should_next = false;
    }

    auto Debugger::enable_if_needed() -> void
    {
        if (should_enable) {
            should_enable = false;
            enable();
        }
    }

    auto Debugger::render() -> void
    {
        std::scoped_lock lock(context_mutex);

        bool position_updated = context && m_last_code != context->stack->Code();
        if (position_updated && !should_next) {
            if (auto ctx = context) {
                DWORD bytes_written;
                Output::send<LogLevel::Warning>(STR("[BlueprintDebugger]: breakpoint hit\n"));
                UFunction* node = ctx->stack->Node();
                size_t index = ctx->stack->Code() - node->GetScript().GetData() - 1;

                // TODO: send UTF8 instead of UTF16
                std::wstring msg = L"DEBUGGER - BREAKPOINT HIT | " + node->GetPathName() + L" | " + std::to_wstring(index) + L" | " + ctx->context->GetPathName();

                msg += L" | STACK";
                FFrame* current = ctx->stack;
                for (int i = 0; current != nullptr; ++i)
                {
                    msg += L" | " + current->Node()->GetPathName();
                    current = current->PreviousFrame();
                }

                msg += L" | LOCALS";

                for (FProperty* property : TFieldRange<FProperty>(node, EFieldIterationFlags::IncludeDeprecated))
                {
                    FString text{};
                    auto container_ptr = property->ContainerPtrToValuePtr<void*>(context->stack->Locals());
                    property->ExportTextItem(text, container_ptr, container_ptr, static_cast<UObject*>(node), NULL);

                    msg += L" | " + property->GetName() + L" | " + *text;
                }

                WriteFile(hPipe, msg.c_str(), msg.size() * sizeof(wchar_t), &bytes_written, NULL);
                FlushFileBuffers(hPipe);
            }
        }


        if (!is_hooked && ImGui::Button("enable"))
        {
            should_enable = true;

            SetConsoleOutputCP(CP_UTF8);
            SetConsoleCP(CP_UTF8);

            hPipe = CreateNamedPipeA("\\\\.\\pipe\\BPDebuggerPipe", PIPE_ACCESS_DUPLEX, PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT, 1, 4096, 4096, 0, NULL);
            if (hPipe == INVALID_HANDLE_VALUE)
            {
                return;
            }

            STARTUPINFOA si;
            ZeroMemory(&si, sizeof(si));
            si.cb = sizeof(si);

            ZeroMemory(&piProcInfo, sizeof(PROCESS_INFORMATION));

            std::ifstream file(m_uebpv_path);
            std::string str = "";
            std::getline(file, str);
            file.close();
            char* cmdLine = new char[str.length() + 1];
            strcpy(cmdLine, str.c_str());
            std::string str2 = std::filesystem::path{ cmdLine }.parent_path().string();
            char* cmdDir = new char[str2.length() + 1];
            strcpy(cmdDir, str2.c_str());

            bool success = CreateProcessA(NULL, cmdLine, NULL, NULL, TRUE, 0, NULL, cmdDir, &si, &piProcInfo);
            if (!success)
            {
                return;
            }

            is_pipe_connected = ConnectNamedPipe(hPipe, NULL) ? true : (GetLastError() == ERROR_PIPE_CONNECTED);
        }

        if (is_hooked && is_pipe_connected)
        {
            wchar_t ch;
            DWORD bytes_read;
            std::wstring current_message = L"";

            while (ReadFile(hPipe, &ch, 1, &bytes_read, NULL) && bytes_read > 0) {
                if (ch == L'\n') {
                    if (current_message.starts_with(L"DEBUGGER - ADD BREAKPOINT"))
                    {
                        std::vector<std::wstring> parts = split_wstring(current_message, L" | ");
                        Output::send<LogLevel::Warning>(STR("[BlueprintDebugger]: add breakpoint {}, {}\n"), parts[1], parts[2]);
                        g_breakpoints.add_breakpoint(parts[1], _wtoi(parts[2].c_str()));
                    }
                    else if (current_message.starts_with(L"DEBUGGER - REMOVE BREAKPOINT"))
                    {
                        std::vector<std::wstring> parts = split_wstring(current_message, L" | ");
                        Output::send<LogLevel::Warning>(STR("[BlueprintDebugger]: add breakpoint {}, {}\n"), parts[1], parts[2]);
                        g_breakpoints.remove_breakpoint(parts[1], _wtoi(parts[2].c_str()));
                    }
                    else if (current_message.starts_with(L"DEBUGGER - UNPAUSE"))
                    {
                        should_pause = false;
                    }
                    else if (current_message.starts_with(L"DEBUGGER - NEXT"))
                    {
                        if (context)
                        {
                            next_func = context->stack->Node();
                        }
                        should_next = true;
                    }
                    else if (current_message.starts_with(L"DEBUGGER - SET VALUE"))
                    {
                        if (context)
                        {
                            std::vector<std::wstring> parts = split_wstring(current_message, L" | ");
                            for (FProperty* property : TFieldRange<FProperty>(context->stack->Node(), EFieldIterationFlags::IncludeDeprecated))
                            {
                                if (property->GetName() == parts[1])
                                {
                                    auto container_ptr = property->ContainerPtrToValuePtr<void*>(context->stack->Locals());
                                    property->ImportText_InContainer(parts[2].c_str(), container_ptr, static_cast<UObject*>(context->stack->Node()), NULL, NULL);
                                }

                            }
                        }
                    }
                    
                    current_message = L"";
                    break;
                }
                else if (ch != L'\r') {
                    current_message += ch;
                }
            }

            if (bytes_read == 0)
            {
                disable();
                CloseHandle(hPipe);
            }
        }


        if (is_hooked)
        {
            if (auto ctx = context)
            {
                m_last_code = ctx->stack->Code();
            }
        }

        if (!is_hooked || !context)
            m_last_code = nullptr;
    }

}
