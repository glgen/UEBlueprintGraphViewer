using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UEBlueprintGraphViewer.Assets;
using UEBlueprintGraphViewer.Decompiler;
using UEBlueprintGraphViewer.Engine;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer
{
    public class FunctionData
    {
        public string Name;
        public List<PropertyData> Params = [];

        public EFunctionFlags Flags;
        public bool IsPure => Flags.HasFlag(EFunctionFlags.FUNC_BlueprintPure);

        public ObjectData Outer;
        
        public FunctionData(string name, EFunctionFlags flags)
        {
            Name = name;
            Flags = flags;
        }

        public override string ToString() => Name;
    }

    public class ObjectData
    {
        public string Name;
        public string PathName;
        public ObjectData? Outer;
        public ObjectData[]? Interfaces;
        private Dictionary<string, PropertyData> properties = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, FunctionData> functions = new(StringComparer.OrdinalIgnoreCase);

        public ObjectData(string name, string pathName)
        {
            Name = name == EngineBPData.None ? EngineBPData.None : name;
            PathName = pathName;
        }

        public override string ToString() => Name;
        
        public PropertyData? GetObjectProperty(string name)
        {
            ObjectData obj = this;
            while (obj != null)
            {
                PropertyData? prop = obj!.properties.GetValueOrDefault(name);
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
                props.AddRange(obj.properties.Values);
                obj = obj.Outer;
            }
            return props;
        }

        public void AddProperty(PropertyData prop) => properties.TryAdd(prop.Name, prop);

        public void AddFunction(FunctionData func) => functions.TryAdd(func.Name, func);
    }

    public class EnumData
    {
        public string Name;
        public Dictionary<long, string> Elements = [];

        public EnumData(string name)
        {
            Name = name;
        }
    }

    public class ParamMappings
    {
        private readonly Dictionary<string, ObjectData> ObjectsLookup = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ObjectData> ObjectsPathLookup = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<EnumData> Enums = [];
        private readonly HashSet<string> ObjectNamesCollisions = [];

        public ParamMappings(string filePath)
        {
            ParseFile(filePath);
        }

        public bool IsObjectNameUnique(string name)
        {
            return !ObjectNamesCollisions.Contains(name);
        }

        public FunctionData GetFunction(string ObjectName, string FuncName)
        {
            if (string.IsNullOrEmpty(ObjectName))
                throw new DecompilerException("ObjectName is empty, failed to get function mappings");

            if (FindObject(ObjectName) is ObjectData obj && FindFunctionRecursive(obj, FuncName, out FunctionData? data))
                return data!;

            throw new DecompilerException($"Failed to find function {FuncName} on object {ObjectName} to get it's properties");
        }

        public FunctionData GetFunctionPathName(string ObjectPathName, string FuncName)
        {
            if (FindObjectPathName(ObjectPathName) is ObjectData obj && FindFunctionRecursive(obj, FuncName, out FunctionData? data))
                return data!;

            throw new DecompilerException($"Failed to find function {FuncName} on object {ObjectPathName} to get it's properties");
        }

        private static bool FindFunctionRecursive(ObjectData obj, string FuncName, out FunctionData? func)
        {
            func = obj.GetFunction(FuncName);
            if (func != null)
                return true;

            if (obj.Outer != null && FindFunctionRecursive(obj.Outer, FuncName, out func))
                return true;

            if (obj.Interfaces != null)
            {
                foreach (var inter in obj.Interfaces)
                {
                    if (FindFunctionRecursive(inter, FuncName, out func))
                        return true;
                }
            }
            return false;
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

        private ObjectData? FindObject(string name)
        {
            return ObjectsLookup.GetValueOrDefault(name);
        }

        private ObjectData? FindObjectPathName(string name)
        {
            return ObjectsPathLookup.GetValueOrDefault(AssetsUtils.FixAssetPath(name));
        }

        private bool TryFindObject(FPackageIndex objIndex, out ObjectData? obj)
        {
            string name = PackageIndexToName(objIndex);
            if (IsObjectNameUnique(name))
            {
                obj = FindObject(name);
            }
            else
            {
                obj = FindObjectPathName(objIndex.ResolvedObject!.GetPathName());
            }
            return obj != null;
        }

        public EnumData? TryFindEnum(string Name)
        {
            return Enums.Find(o => o.Name.EqualsFName(Name));
        }
        
        private void ParseFile(string FilePath)
        {
            StreamReader reader = new StreamReader(FilePath);
            HashSet<string> objectNames = [];
            List<string> parents = [];
            List<string[]?> interfaces = [];
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                string[] Parts = line.Split(" | ");

                if (Parts[0] == "OBJECT")
                {
                    var obj = new ObjectData(Parts[1].Trim(), Parts[2].Trim());
                    bool collides = objectNames.Contains(obj.Name);
                    if (collides)
                    {
                        ObjectNamesCollisions.Add(obj.Name);
                    }
                    else
                    {
                        objectNames.Add(obj.Name);
                    }
                    parents.Add(Parts[3]);
                    interfaces.Add(Parts[4] == EngineBPData.None ? null : Parts[4].Split(" "));

                    while ((line = reader.ReadLine()) != "END OBJECT")
                    {
                        string[] parts = line!.Split(" | ");

                        if (parts[0] == "  PROPERTY")
                        {
                            obj.AddProperty(CreatePropertyData(parts, obj.PathName));
                        }
                        else if (parts[0] == " FUNCTION")
                        {
                            FunctionData func = new(parts[1].Trim(), (EFunctionFlags)Convert.ToUInt32(parts[2]));

                            while ((line = reader.ReadLine()) != " END FUNCTION")
                                func.Params.Add(CreatePropertyData(line!.Split(" | "), func.Name));

                            func.Outer = obj;
                            obj.AddFunction(func);
                        }
                    }

                    ObjectsPathLookup.Add(obj.PathName, obj);
                    if (!collides)
                        ObjectsLookup.Add(obj.Name, obj);
                }
                else if (Parts[0] == "ENUM")
                {
                    EnumData enumData = new EnumData(Parts[1]);

                    while ((line = reader.ReadLine()) != "END ENUM")
                    {
                        string[] parts = line!.Split(" | ");
                        enumData.Elements.TryAdd(Convert.ToInt64(parts[1]), parts[2]);
                    }

                    Enums.Add(enumData);
                }
            }

            reader.Close();

            // resolve parents and interfaces
            var objs = ObjectsPathLookup.Values.ToArray();
            for (int i = 0; i < objs.Length; i++)
            {
                if (parents[i] != EngineBPData.None)
                    objs[i].Outer = FindObjectPathName(parents[i]);
                if (interfaces[i] != null)
                    objs[i].Interfaces = [.. interfaces[i]!.Select(FindObjectPathName)!];
            }

            static PropertyData CreatePropertyData(string[] parts, string ownerName)
            {
                EPropertyFlags propFlags = (EPropertyFlags)Convert.ToUInt64(parts[3]);
                return new PropertyData(parts[2].Trim(), ownerName, parts[1], propFlags, parts[4], parts[5], parts[6]);
            }
        }
    }
}
