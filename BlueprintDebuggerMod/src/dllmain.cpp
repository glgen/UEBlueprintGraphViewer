#include <string>

#include <GUI/GUITab.hpp>
#include <Mod/CppUserModBase.hpp>
#include <UE4SSProgram.hpp>
#include <BlueprintDebugger.hpp>

class BlueprintDebuggerMod : public RC::CppUserModBase
{
private:
    RC::GUI::KismetDebuggerMod::Debugger m_debugger{};

public:
    BlueprintDebuggerMod() : CppUserModBase()
    {
        ModName = STR("BlueprintDebugger");
        ModVersion = STR("1.0");
        ModDescription = STR("Debugging interface for kismet bytecode");
        ModAuthors = STR("glgen (based on trumank's KismetDebugger)");

        register_tab(STR("Blueprint Debugger"), [](CppUserModBase* mod) {
            UE4SS_ENABLE_IMGUI()
            dynamic_cast<BlueprintDebuggerMod*>(mod)->m_debugger.render();
        });
    }

    auto on_update() -> void override
    {
        m_debugger.enable_if_needed();
    }

    ~BlueprintDebuggerMod() override = default;
};

#define KISMET_DEBUGGER_MOD_API __declspec(dllexport)
extern "C"
{
    KISMET_DEBUGGER_MOD_API RC::CppUserModBase* start_mod()
    {
        return new BlueprintDebuggerMod();
    }

    KISMET_DEBUGGER_MOD_API void uninstall_mod(RC::CppUserModBase* mod)
    {
        delete mod;
    }
}

