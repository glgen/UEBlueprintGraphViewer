#include <format>
#include <Mod/CppUserModBase.hpp>
#include <DynamicOutput/DynamicOutput.hpp>
#include <Unreal/UObjectGlobals.hpp>
#include <Unreal/UObject.hpp>
#include <Unreal/UFunction.hpp>
#include <Unreal/FProperty.hpp>
#include <Unreal/UAssetRegistry.hpp>
#include <Unreal/UStruct.hpp>
#include <Unreal/UClass.hpp>
#include <Unreal/UScriptStruct.hpp>
#include <Unreal/UEnum.hpp>
#include <Unreal/Property/FObjectProperty.hpp>
#include <Unreal/Property/FClassProperty.hpp>
#include <Unreal/Property/FSoftClassProperty.hpp>
#include <Unreal/Property/FInterfaceProperty.hpp>
#include <Unreal/Property/FStructProperty.hpp>
#include <Unreal/Property/FEnumProperty.hpp>
#include <Unreal/Property/FArrayProperty.hpp>
#include <Unreal/Property/FMapProperty.hpp>
#include <Unreal/Property/FSetProperty.hpp>
#include <Unreal/Property/NumericPropertyTypes.hpp>
#include <Unreal/UKismetNodeHelperLibrary.hpp>
#include <UE4SSProgram.hpp>
using namespace std;
using namespace RC::Unreal;

class UE_FunctionsDump : public RC::CppUserModBase
{
private:
    const char* state = "";
    bool loadAllAssets = false;
public:
    UE_FunctionsDump() : CppUserModBase()
    {
        ModName = STR("UE_FunctionsDump");
        ModVersion = STR("1.0");
        ModDescription = STR("UE Function Dump");
        ModAuthors = STR("glgen");

        register_tab(STR("UE Function Dump"), [](CppUserModBase* instance) {
            UE4SS_ENABLE_IMGUI()
            ImGui::Text("Dump file will be saved as dump.txt in Win64 folder");

            auto mod = dynamic_cast<UE_FunctionsDump*>(instance);
            if (!mod)
            {
                return;
            }

            ImGui::Checkbox("Load all assets into memory", &(mod->loadAllAssets));

            if (ImGui::Button("Dump"))
            {
                if (mod->loadAllAssets)
                {
                    mod->state = "Loading all assets...";
                    UAssetRegistry::LoadAllAssets();
                }

                mod->state = "Dumping...";
                mod->dump();
                mod->state = "Done";
            }

            ImGui::Text(mod->state);

        });

    }

    ~UE_FunctionsDump() override = default;

    auto dump() -> void
    {
        Output::Targets<Output::NewFileDevice> scoped_dumper_out;
        auto& file_device = scoped_dumper_out.get_device<Output::NewFileDevice>();
        file_device.set_file_name_and_path(STR("dump.txt"));
        file_device.set_formatter([](File::StringViewType string) -> File::StringType {
            return File::StringType{ string };
        });

        StringType out_line;
        out_line.reserve(200000000);

        UObjectGlobals::ForEachUObject([&](void* object, int32_t chunk_index, int32_t object_index)
        {
            UObject* obj = static_cast<UObject*>(object);

            if (obj->IsA<UEnum>())
            {
                out_line.append(fmt::format(STR("ENUM | {}\n"), obj->GetName()));
                UEnum* obj_enum = static_cast<UEnum*>(obj);

                for (TPair<FName, int64> pair : obj_enum->GetEnumNames())
                {
                    StringType name = UKismetNodeHelperLibrary::GetEnumeratorUserFriendlyName(obj_enum, pair.Value);
                    out_line.append(fmt::format(STR(" ENUM ELEM | {} | {}\n"), pair.Value, name));
                }
                out_line.append(STR("END ENUM\n"));
            }


            if (obj->IsA<UStruct>() && !obj->IsA<UFunction>())
            {
                UStruct* obj_struct = static_cast<UStruct*>(obj);
                StringType name = obj->GetName();
                StringType path_name = obj->GetPathName();
                UObject* outer_obj = obj_struct->GetSuperStruct();
                StringType outer = STR("None");
                if (outer_obj != nullptr)
                {
                    outer = outer_obj->GetPathName();
                }
                StringType interfaces = STR("None");
                if (obj->IsA<UClass>())
                {
                    UClass* obj_class = static_cast<UClass*>(obj);
                    TArray<FImplementedInterface, TSizedDefaultAllocator<32>> interfaces_arr = obj_class->GetInterfaces();
                    StringType interfaces_str;
                    for (FImplementedInterface interface : interfaces_arr)
                    {
                        interfaces_str.append(interface.Class->GetPathName() + STR(" "));
                    }

                    if (!interfaces_str.empty())
                    {
                        interfaces_str.pop_back();
                        interfaces = interfaces_str;
                    }
                }

                out_line.append(fmt::format(STR("OBJECT | {} | {} | {} | {}\n"), name, path_name, outer, interfaces));

                if (obj_struct->GetClassPrivate()->HasAnyClassFlags(CLASS_Native))
                {
                    DumpProperties(obj_struct, out_line);
                }

                for (UFunction* func : obj_struct->ForEachFunction())
                {
                    StringType func_name = func->GetName();
                    StringType func_flags = to_wstring((uint32_t)func->GetFunctionFlags());

                    out_line.append(fmt::format(STR(" FUNCTION | {} | {}\n"), func_name, func_flags));

                    DumpProperties(func, out_line);

                    out_line.append(STR(" END FUNCTION\n"));
                }
                out_line.append(STR("END OBJECT\n"));
            }
            return LoopAction::Continue;
        });

        scoped_dumper_out.send(out_line);
    }

    auto DumpProperties(UStruct* struc, StringType& out_line) -> void
    {
        for (FProperty* prop : struc->ForEachProperty())
        {
            StringType prop_name = prop->GetName();
            StringType prop_type = prop->GetClass().GetName();
            StringType prop_flags = to_wstring((uint32_t)prop->GetPropertyFlags());
            StringType prop_class_name = STR("None");
            StringType prop_inner_prop = STR("None");
            StringType prop_value_prop = STR("None");

            if (prop->IsA<FClassProperty>())
            {
                FClassProperty* casted_prop = static_cast<FClassProperty*>(prop);
                prop_class_name = GetNameChecked(casted_prop->GetMetaClass());
            }
            else if (prop->IsA<FSoftClassProperty>())
            {
                FClassProperty* casted_prop = static_cast<FClassProperty*>(prop);
                prop_class_name = GetNameChecked(casted_prop->GetMetaClass());
            }
            else if (prop->IsA<FObjectPropertyBase>())
            {
                FObjectPropertyBase* casted_prop = static_cast<FObjectPropertyBase*>(prop);
                prop_class_name = GetNameChecked(casted_prop->GetPropertyClass());
            }
            else if (prop->IsA<FInterfaceProperty>())
            {
                FInterfaceProperty* casted_prop = static_cast<FInterfaceProperty*>(prop);
                prop_class_name = GetNameChecked(casted_prop->GetInterfaceClass());
            }
            else if (prop->IsA<FStructProperty>())
            {
                FStructProperty* casted_prop = static_cast<FStructProperty*>(prop);
                prop_class_name = GetNameChecked(casted_prop->GetStruct());
            }
            else if (prop->IsA<FByteProperty>())
            {
                FByteProperty* casted_prop = static_cast<FByteProperty*>(prop);
                if (casted_prop->IsEnum())
                {
                    prop_class_name = GetNameChecked(casted_prop->GetEnum());
                }
            }
            else if (prop->IsA<FEnumProperty>())
            {
                FEnumProperty* casted_prop = static_cast<FEnumProperty*>(prop);
                prop_class_name = GetNameChecked(casted_prop->GetEnum());
            }
            else if (prop->IsA<FArrayProperty>())
            {
                FArrayProperty* casted_prop = static_cast<FArrayProperty*>(prop);
                prop_inner_prop = casted_prop->GetInner()->GetClass().GetName();
            }
            else if (prop->IsA<FMapProperty>())
            {
                FMapProperty* casted_prop = static_cast<FMapProperty*>(prop);
                prop_inner_prop = casted_prop->GetKeyProp()->GetClass().GetName();
                prop_value_prop = casted_prop->GetValueProp()->GetClass().GetName();
            }
            else if (prop->IsA<FSetProperty>())
            {
                FSetProperty* casted_prop = static_cast<FSetProperty*>(prop);
                prop_inner_prop = casted_prop->GetElementProp()->GetClass().GetName();
            }

            out_line.append(fmt::format(STR("  PROPERTY | {} | {} | {} | {} | {} | {}\n"), prop_type, prop_name, prop_flags, prop_class_name, prop_inner_prop, prop_value_prop));
        }
    }

    auto GetNameChecked(UObject* obj) -> StringType
    {
        if (obj)
        {
            return obj->GetName();
        }
        return STR("None");
    }
};

#define MY_AWESOME_MOD_API __declspec(dllexport)
extern "C"
{
    MY_AWESOME_MOD_API RC::CppUserModBase* start_mod()
    {
        return new UE_FunctionsDump();
    }

    MY_AWESOME_MOD_API void uninstall_mod(RC::CppUserModBase* mod)
    {
        delete mod;
    }
}