using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.UObject;

namespace UEBlueprintGraphViewer.Engine;

public class ObjectData
{
    public string Name;
    public ObjectData? Outer;
    public ObjectData? SuperStruct;
    public ObjectData[]? Interfaces = [];
    public Dictionary<string, PropertyData> Properties = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, FunctionData> functions = new(StringComparer.OrdinalIgnoreCase);

    public UObject? Object = null;
    
    public ObjectData()
    {
        
    }
    
    public ObjectData(UObject obj)
    {
        Name = obj.GetPathName();
        if (obj.Outer != null)
            Outer = new ObjectData(obj.Outer.Load());
        Object = obj;
        if (obj is UClass c)
        {
            Interfaces = c.Interfaces?.Select(o => new ObjectData(o.Class.Load()!)).ToArray();
            Properties = c.ChildProperties?.Select(o => new PropertyData(new PropertyContainer((o as FProperty)!), obj)).ToDictionary(o => o.Name, o => o, StringComparer.OrdinalIgnoreCase) ?? [];
            functions = c.FuncMap?.Select(o =>
            {
                var func = o.Value.Load() as UFunction;
                return new FunctionData(func.Name, func.FunctionFlags) { Outer = this };
            }).ToDictionary(o => o.Name, o => o, StringComparer.OrdinalIgnoreCase);
        }
    }

    public override string ToString() => Name;
        
    public PropertyData? GetObjectProperty(string name)
    {
        ObjectData obj = this;
        while (obj != null)
        {
            PropertyData? prop = obj!.Properties.GetValueOrDefault(name);
            if (prop != null) return prop;
            obj = obj.Outer;
        }
        return null;
    }
        
    public FunctionData? GetFunction(string name)
    {
        return functions.GetValueOrDefault(name);
    }
        
    public List<PropertyData> GetAllProperties()
    {
        List<PropertyData> props = [];
        ObjectData? obj = this;
        while (obj != null)
        {
            props.AddRange(obj.Properties.Values);
            obj = obj.SuperStruct;
        }
        return props;
    }

    public void AddProperty(PropertyData prop) => Properties.TryAdd(prop.Name, prop);

    public void AddFunction(FunctionData func) => functions.TryAdd(func.Name, func);
}