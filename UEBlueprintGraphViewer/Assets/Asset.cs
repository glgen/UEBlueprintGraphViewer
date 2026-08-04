using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.Utils;
using UEBlueprintGraphViewer.Engine;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.Assets
{
    public class Asset
    {
        public string ObjectName = null!;
        public string Name = null!;
        public bool IsBP;
        public UBlueprintGeneratedClass? GeneratedClass;

        public UFunction? UbergraphFunction;

        private readonly List<UFunction> _events = [];
        public List<UFunction> SortedEvents = [];
        public Dictionary<string, UFunction> Events = [];
        public readonly List<UFunction> Functions = [];
        public readonly Dictionary<string, PropertyData> LoadedProperties = new(StringComparer.OrdinalIgnoreCase);

        public readonly List<InputEventData> InputEvents = [];
        public readonly List<TimelineData> Timelines = [];
        
        public Asset(IPackage package, string name)
        {
            Name = package.Name;
            
            // try to find generated class with the name "{FileName}_C"
            int index = package.GetExportIndex($"{name}_C", StringComparison.OrdinalIgnoreCase);
        
            // if generated class have a different name from filename,
            // trying to find export with generated class
            if (index < 0)
                index = GetBPClassIndex(package);
            
            if (index < 0 || package.ExportsLazy[index].Value is not UBlueprintGeneratedClass BPClass)
                return;
        
            IsBP = true;
            GeneratedClass = BPClass;
            ProcessBPClass();
        }

        public bool IsEvent(UFunction? func) => _events.Contains(func);

        private void ProcessBPClass()
        {
            ObjectName = GeneratedClass!.Name;
            var functions = GeneratedClass.FuncMap.Select(o => o.Value.ResolvedObject?.Object?.Value).OfType<UFunction>().ToArray();
            
            // finding ubergraph
            UbergraphFunction = functions.FirstOrDefault(o => o.FunctionFlags.HasFlag(EFunctionFlags.FUNC_UbergraphFunction));

            // finding events/functions
            foreach (UFunction func in functions)
            {
                // skip delegate signatures and ubergraph itself
                if (func.FunctionFlags.HasFlag(EFunctionFlags.FUNC_Delegate) || func == UbergraphFunction)
                    continue;

                if (IsUbergraphEvent(func.ScriptBytecode))
                    _events.Add(func);
                else
                    Functions.Add(func);
            }

            SortedEvents = Settings.Instance.ReorderEvents ? _events.OrderBy(o => GetUbergraphEntryPoint(o.ScriptBytecode)).ToList() : _events;
            Events = SortedEvents.ToDictionary(o => o.Name, o => o);

            if (GetPropValue<UScriptArray>(GeneratedClass, "DynamicBindingObjects") is { } array)
            {
                foreach (var index in GetPropsValues<FPackageIndex>(array))
                {
                    if (index.ResolvedObject?.Object?.Value.Class?.Name.ToString() == "ComponentDelegateBinding")
                    {
                        // TODO
                    }
                    else
                    {
                        InputEvents.AddRange(GetInputEvents(index.ResolvedObject?.Object?.Value));
                    }
                }
            }

            foreach (var inputEvent in InputEvents.Where(o => o.FunctionName != "None"))
            {
                Events.Remove(inputEvent.FunctionName);
                if (!Events.ContainsKey(inputEvent.Name))
                {
                    Events[inputEvent.Name] = _events.Find(o => o.Name == inputEvent.FunctionName)!;
                }
            }

            if (GetPropValue<UScriptArray>(GeneratedClass, "Timelines") is { } array2)
            {
                foreach (var index in GetPropsValues<FPackageIndex>(array2))
                {
                    Timelines.Add(GetTimelineData(index.ResolvedObject?.Object?.Value));
                }
            }

            foreach (var timeline in Timelines)
            {
                Events.Remove(timeline.UpdateFunctionName);
                Events.Remove(timeline.FinishedFunctionName);
            }

            T? GetPropValue<T>(AbstractPropertyHolder? obj, string name) where T : class
            {
                return obj?.Properties.FirstOrDefault(o => o.Name.ToString() == name)?.Tag?.GetValue(typeof(T)) as T;
            }
            
            string GetPropValueName(AbstractPropertyHolder? obj, string name)
            {
                return GetPropValue<object>(obj, name)?.ToString() ?? "None";
            }
            
            IEnumerable<T> GetPropsValues<T>(UScriptArray? obj) where T : class
            {
                return obj?.Properties.Select(o => o.GenericValue).OfType<T>() ?? [];
            }

            List<InputEventData> GetInputEvents(UObject? obj)
            {
                List<InputEventData> result = [];
                InputEventType eventType = obj?.Class?.Name.ToString() switch
                {
                    "InputKeyDelegateBinding" => InputEventType.Key,
                    "InputAxisKeyDelegateBinding" => InputEventType.InputAxisKey,
                    "InputActionDelegateBinding" => InputEventType.InputAction,
                    "InputAxisDelegateBinding" => InputEventType.InputAxisAction,
                    "EnhancedInputActionDelegateBinding" => InputEventType.EnhancedInputAction,
                    _ => InputEventType.Key,
                };
                
                if (obj?.Properties.FirstOrDefault()?.Tag?.GenericValue is UScriptArray eventsArray)
                {
                    foreach (var eventInfo in GetPropsValues<FScriptStruct>(eventsArray).Select(o => o.StructType).OfType<FStructFallback>())
                    {
                        string funcName = GetPropValueName(eventInfo, "FunctionNameToBind");
                        
                        string name = eventType switch
                        {
                            InputEventType.Key => funcName.SubstringBefore("_K2Node_").SubstringAfter("InpActEvt_").Replace('_', ' '),
                            InputEventType.InputAxisKey => funcName.SubstringBefore("_K2Node_").SubstringAfter("InpAxisKeyEvt_").Replace('_', ' '),
                            InputEventType.InputAction => $"InputAction {GetPropValueName(eventInfo, "InputActionName")}",
                            InputEventType.InputAxisAction => $"InputAxis {GetPropValueName(eventInfo, "InputAxisName")}",
                            InputEventType.EnhancedInputAction => $"EnhancedInputAction {GetPropValue<FPackageIndex>(eventInfo, "InputAction")?.Name ?? "None"}",
                            _ => throw new ArgumentOutOfRangeException(),
                        };

                        InputEventPinType pinType;
                        
                        switch (eventType)
                        {
                            case InputEventType.Key:
                            case InputEventType.InputAction:
                            {
                                string type = GetPropValueName(eventInfo, "InputKeyEvent");
                                pinType = type switch
                                {
                                    "EInputEvent::IE_Pressed" or "IE_Pressed" => InputEventPinType.Pressed,
                                    "EInputEvent::IE_Released" or "IE_Released" => InputEventPinType.Released,
                                    _ => throw new ArgumentOutOfRangeException(),
                                };
                                break;
                            }
                            case InputEventType.InputAxisAction:
                            case InputEventType.InputAxisKey:
                            {
                                pinType = InputEventPinType.AxisExec;
                                break;
                            }
                            case InputEventType.EnhancedInputAction:
                            {
                                string type = GetPropValueName(eventInfo, "TriggerEvent");
                                pinType = type switch
                                {
                                    "ETriggerEvent::Triggered" => InputEventPinType.Triggered,
                                    "ETriggerEvent::Started" => InputEventPinType.Started,
                                    "ETriggerEvent::Ongoing" => InputEventPinType.Ongoing,
                                    "ETriggerEvent::Canceled" => InputEventPinType.Canceled,
                                    "ETriggerEvent::Completed" => InputEventPinType.Completed,
                                    _ => throw new ArgumentOutOfRangeException(),
                                };
                                break;
                            }
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        
                        result.Add(new() { Name = name, FunctionName = funcName, PinType = pinType, Type = eventType});
                    }
                }

                return result;
            }

            TimelineData GetTimelineData(UObject? obj)
            {
                List<FloatTrack> floatTracks = [];
                if (GetPropValue<UScriptArray>(obj, "FloatTracks") is { } array)
                {
                    floatTracks.AddRange(GetPropsValues<FScriptStruct>(array)
                        .Select(o => o.StructType)
                        .OfType<FStructFallback>()
                        .Select(track => new FloatTrack()
                        {
                            //CurveFloat = GetPropValue<FPackageIndex>(track, "CurveFloat").ResolvedObject.Object.Value,
                            PropertyName = GetPropValueName(track, "PropertyName"),
                            TrackName = GetPropValueName(track, "TrackName"),
                            IsExternalCurve = bool.Parse(GetPropValue<object>(track, "bIsExternalCurve")?.ToString() ?? "false")
                        }));
                }

                return new()
                {
                    FloatTracks = floatTracks,
                    TimelineGuid = GetPropValueName(obj, "TimelineGuid"),
                    VariableName = GetPropValueName(obj, "VariableName"),
                    DirectionPropertyName = GetPropValueName(obj, "DirectionPropertyName"),
                    UpdateFunctionName = GetPropValueName(obj, "UpdateFunctionName"),
                    FinishedFunctionName = GetPropValueName(obj, "FinishedFunctionName"),
                };
            }
        }

        bool IsUbergraphEvent(KismetExpression[] script)
        {
            if (script.Length < 3 || UbergraphFunction == null)
                return false;
            
            return script[^3] switch
            {
                EX_LocalFinalFunction ex => ex.StackNode.IsExport && ex.StackNode.Load() == UbergraphFunction,
                EX_VirtualFunction ex => ex.VirtualFunctionName == UbergraphFunction.Name,
                _ => false,
            };
        }

        // Method to find index of generated class export index.
        // We don't want to load all exports to check their classes
        // so instead we resolve class names from ExportMap
        // (this will not load the exports themselves)
        static int GetBPClassIndex(IPackage package)
        {
            switch (package)
            {
                case Package pack:
                    for (int i = 0; i < pack.ExportMap.Length; i++)
                    {
                        if (pack.ExportMap[i].ClassName.Ends("BlueprintGeneratedClass"))
                            return i;
                    }
                    break;
                case IoPackage io:
                    for (int i = 0; i < io.ExportMap.Length; i++)
                    {
                        var classIndex = io.ResolveObjectIndex(io.ExportMap[i].ClassIndex)?.Object?.Value as UStruct;
                        if (classIndex?.Name.Ends("BlueprintGeneratedClass") == true)
                            return i;
                    }
                    break;
            }
            return -1;
        }

        public void LoadAllProperties()
        {
            if (GeneratedClass == null) return;
            foreach (var prop in GeneratedClass.ChildProperties.OfType<FProperty>().Select(o => new PropertyData(o, GeneratedClass)))
                LoadedProperties.Add(prop.Name, prop);
        }
    }

    public class AssetIsNotBlueprintException : Exception
    {
        public AssetIsNotBlueprintException() : base("Asset is not blueprint") { }
    }
}
