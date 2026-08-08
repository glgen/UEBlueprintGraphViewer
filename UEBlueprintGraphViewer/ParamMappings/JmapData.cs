using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.Utils;
using UEBlueprintGraphViewer.Decompiler;
using UEBlueprintGraphViewer.Engine;

namespace UEBlueprintGraphViewer;

public class JmapData
{
    private readonly Dictionary<string, ObjectData> _objectsLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FunctionData> _functionsLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<EnumData> _enums = [];

    public JmapData(string path)
    {
        Read(path);
    }
    
    public void Read(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read);
        using Stream reader = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(file, CompressionMode.Decompress)
            : file;
        var result = JsonSerializer.Deserialize<JmapJson>(reader);

        foreach (var obj in result.Objects)
        {
            if (obj.Value.Type is "Class" or "Package" or "ScriptStruct")
            {
                _objectsLookup.Add(obj.Key, new ObjectData()
                {
                    Name = obj.Key,
                    Properties = obj.Value.Properties?.Select(o => GetProperty(o, obj.Key)!)?.ToDictionary(o => o.Name, o => o, StringComparer.OrdinalIgnoreCase) ?? [],
                });
            }
            if (obj.Value.Type == "Function")
            {
                // removing outers from name
                string name = obj.Key.Substring(obj.Value.Outer.Length + 1);
                _functionsLookup.Add(obj.Key, new FunctionData(name, GetFunctionFlags(obj.Value.FunctionFlags))
                {
                    Params = [.. obj.Value.Properties?.Select(o => GetProperty(o, obj.Key))!],
                });
            }
            if (obj.Value.Type == "Enum")
            {
                _enums.Add(new EnumData(obj.Key)
                {
                    Elements = obj.Value.Names?.ToDictionary(
                        o => (o[0] is JsonElement ? (JsonElement)o[0] : default).GetString(),
                        o => (o[1] is JsonElement ? (JsonElement)o[1] : default).GetInt64())
                });
            }
        }

        foreach (var obj in result.Objects)
        {
            if (obj.Value.Type is "Class" or "ScriptStruct")
            {
                var toChange = _objectsLookup[obj.Key];
                
                toChange.Outer = _objectsLookup[obj.Value.Outer];
                if (obj.Value.SuperStruct != null)
                    toChange.SuperStruct = _objectsLookup[obj.Value.SuperStruct];
                toChange.Interfaces = obj.Value.Intefaces?.Select(o => _objectsLookup[o.Class]).ToArray() ?? [];
            }
            if (obj.Value.Type == "Function")
            {
                var outer = _objectsLookup[obj.Value.Outer];
                var func = _functionsLookup[obj.Key];
                outer.AddFunction(func);
                func.Outer = _objectsLookup[obj.Value.Outer];
            }
        }
    }

    private PropertyData? GetProperty(JmapProperty prop, string owner)
    {
        return new PropertyData(
            prop.Name,
            owner,
            prop.Type,
            GetPropertyFlags(prop.Flags),
            prop.PropertyClass,
            prop.Container?.Type ?? prop.Inner?.Type ?? "None",
            prop.KeyProp?.Type ?? "None",
            prop.ValueProp?.Type ?? "None",
            prop.SignatureFunction?.SubstringAfterLast('.') ?? "",
            prop.SignatureFunction?.SubstringBeforeLast('.') ?? "");
    }
    
    private static EFunctionFlags GetFunctionFlags(string flags)
    {
        EFunctionFlags result = 0;
        var parts =  flags.Split(" | ");
        foreach (var flag in parts)
        {
            if (Enum.TryParse(flag, out EFunctionFlags value))
                result |= value;
        }
        return result;
    }
    
    private static EPropertyFlags GetPropertyFlags(string flags)
    {
        EPropertyFlags result = 0;
        var parts =  flags.Split(" | ");
        foreach (var flag in parts)
        {
            if (Enum.TryParse(flag[4..], out EPropertyFlags value))
                result |= value; // remove CPF_ prefix
        }
        return result;
    }

    public FunctionData GetFunctionData(string objName, string funcName)
    {
        if (GetObjectData(objName) is { } data)
        {
            if (FindFunctionRecursive(data, funcName, out var func))
                return func!;
        };
        
        throw new DecompilerException($"JmapData: failed to find function {funcName} in object {objName}");
    }
    
    private static bool FindFunctionRecursive(ObjectData obj, string funcName, out FunctionData? func)
    {
        func = obj.GetFunction(funcName);
        if (func != null)
            return true;

        if (obj.SuperStruct != null && FindFunctionRecursive(obj.SuperStruct, funcName, out func))
            return true;

        if (obj.Interfaces != null)
        {
            foreach (var inter in obj.Interfaces)
            {
                if (FindFunctionRecursive(inter, funcName, out func))
                    return true;
            }
        }
        return false;
    }
    
    public ObjectData? GetObjectData(string objName)
    {
        if (_objectsLookup.TryGetValue(objName, out var obj))
            return obj;
        return null;
    }
    
    public EnumData? TryFindEnum(string Name)
    {
        return _enums.Find(o => o.Name.EqualsFName(Name));
    }
    
    public bool TryFindProperty(string Name, FPackageIndex objIndex, out PropertyData? prop)
    {
        prop = null;
        if (!TryFindObject(objIndex, out ObjectData? obj))
            return false;

        prop = obj!.GetObjectProperty(Name);
        return prop != null;
    }
    
    public bool TryFindProperties(FPackageIndex objIndex, out List<PropertyData>? props)
    {
        props = [];

        if (!TryFindObject(objIndex, out ObjectData? obj))
            return false;

        props = obj!.GetAllProperties();
        return true;
    }
    
    private bool TryFindObject(FPackageIndex objIndex, out ObjectData? obj)
    {
        string name = objIndex.ResolvedObject.GetPathName();
        obj = GetObjectData(name);
        return obj != null;
    }
    
    class JmapJson
    {
        [JsonPropertyName("metadata")]
        public JmapMetadata Metadata { get; set; }
        [JsonPropertyName("image_base_address")]
        public string ImageBaseAddress { get; set; }
        [JsonPropertyName("objects")]
        public Dictionary<string, JmapObject> Objects { get; set; }
    }

    class JmapMetadata
    {
        [JsonPropertyName("tool")]
        public string Tool { get; set; }
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }
        
        [JsonPropertyName("source")]
        public string Source { get; set; }
        
        [JsonPropertyName("engine_version")]
        public JmapEngineVersion EngineVersion { get; set; }
        
        [JsonPropertyName("build_change_list")]
        public string BuildChangeId { get; set; }
    }

    class JmapEngineVersion
    {
        [JsonPropertyName("major")]
        public int Major { get; set; }
        
        [JsonPropertyName("minor")]
        public int Minor { get; set; }
    }
    
    class JmapObject
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        [JsonPropertyName("address")]
        public string Address { get; set; }
        
        [JsonPropertyName("object_flags")]
        public string ObjectFlags { get; set; }
        
        [JsonPropertyName("outer")]
        public string Outer { get; set; }
        
        [JsonPropertyName("class")]
        public string Class { get; set; }
        
        [JsonPropertyName("children")]
        public string[] Children { get; set; }
        
        [JsonPropertyName("property_values")]
        public Dictionary<string, object> PropertyValues { get; set; }
        
        [JsonPropertyName("super_struct")]
        public string? SuperStruct { get; set; }
        
        [JsonPropertyName("properties")]
        public JmapProperty[]? Properties { get; set; }
        
        [JsonPropertyName("function_flags")]
        public string? FunctionFlags { get; set; }
        
        [JsonPropertyName("names")]
        public object[][]? Names { get; set; }
        
        [JsonPropertyName("interfaces")]
        public JmapInterface[]? Intefaces { get; set; }
    }
    
    class JmapProperty
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        [JsonPropertyName("address")]
        public string Address { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("property_class")]
        public string? PropertyClass { get; set; }
        
        [JsonPropertyName("interface_class")]
        public string? InterfaceClass { get; set; }
        
        [JsonPropertyName("flags")]
        public string Flags { get; set; }
        
        [JsonPropertyName("container")]
        public JmapProperty? Container { get; set; }
        
        [JsonPropertyName("key_prop")]
        public JmapProperty? KeyProp { get; set; }
        
        [JsonPropertyName("value_prop")]
        public JmapProperty? ValueProp { get; set; }
        
        [JsonPropertyName("inner")]
        public JmapProperty? Inner { get; set; }
        
        [JsonPropertyName("signature_function")]
        public string? SignatureFunction { get; set; }
    }
    
    class JmapInterface
    {
        [JsonPropertyName("class")]
        public string Class { get; set; }
        
        [JsonPropertyName("implemented_by_k2")]
        public bool ImplementedByK2 { get; set; }
    }
}