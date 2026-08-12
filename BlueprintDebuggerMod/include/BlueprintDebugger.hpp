#pragma once

#include <unordered_map>
#include <unordered_set>

#include <Unreal/FFrame.hpp>
#include <Unreal/CoreUObject/UObject/Class.hpp>
#include <Unreal/UObject.hpp>

namespace RC::GUI::KismetDebuggerMod
{
    using namespace RC::Unreal;
    
    auto expr_to_string(EExprToken expr) -> const char*;

    struct PausedContext
    {
        EExprToken expr{};
        UObject* context{};
        FFrame* stack{};
    };

    class BreakpointStore
    {
    public:
        BreakpointStore();
        ~BreakpointStore();

        auto load(std::filesystem::path& path) -> void;
        auto save() -> void;

        auto has_breakpoint(const StringType& fn, size_t index) -> bool;
        auto add_breakpoint(UFunction* fn, size_t index) -> void;
        auto add_breakpoint(const StringType& fn, size_t index) -> void;
        auto remove_breakpoint(const StringType& fn, size_t index) -> void;

    private:
        typedef std::unordered_set<size_t> FunctionBreakpoints;

        std::unordered_map<UFunction*, std::shared_ptr<FunctionBreakpoints> > m_breakpoints_by_function{};
        std::unordered_map<StringType, std::shared_ptr<FunctionBreakpoints> > m_breakpoints_by_name{};
    };

    class Debugger
    {
    public:
        Debugger();
        ~Debugger();

        auto enable() -> void;
        auto disable() -> void;
        auto enable_if_needed() -> void;

        auto render() -> void;

    private:
        bool m_paused{};
        uint8_t* m_last_code{nullptr}; // pointer to last stack instruction, used to know if it's advanced since last frame
        BreakpointStore& m_breakpoints;
    
    public:
        static inline std::filesystem::path m_save_path;
        static inline std::filesystem::path m_uebpv_path;
    };
}
