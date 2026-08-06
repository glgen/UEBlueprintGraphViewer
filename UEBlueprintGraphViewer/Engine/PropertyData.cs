using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.UObject;
using System;
using System.Linq;
using UEBlueprintGraphViewer.Decompiler;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.EngineEnums;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.Engine
{
    // Object containing property info
    public class PropertyData
    {
        public string Name;

        public EPropertyFlags Flags;

        public UObject? OwnerObject;
        public string Owner;

        public GraphPinType PinType;
        
        public FPackageIndex? PropertyClassPackageIndex;
        public string DelegateSignatureFunction;
        public string DelegateSignatureObjectPath;
        public UObject? DelegateSignatureObject;

        public override string ToString()
        {
            return $"{PinType.ContainerType} {PinType.PinCategory} {Name}";
        }

        public bool IsFunctionParam()
        {
            return Flags.HasFlag(EPropertyFlags.Parm);
        }

        // ReferenceParm always requires OutParam to be set, even if it's not output parameter
        // flag check is taken from https://github.com/EpicGames/UnrealEngine/blob/5.4/Engine/Source/Editor/BlueprintGraph/Private/K2Node_CallFunction.cpp#L1189
        public bool IsOutParam()
        {
            return Flags.HasFlag(EPropertyFlags.ReturnParm) || Flags.HasFlag(EPropertyFlags.OutParm) && !Flags.HasFlag(EPropertyFlags.ReferenceParm);
        }

        public bool IsInputParam()
        {
            return IsFunctionParam() && !IsOutParam();
        }

        public bool IsTempVar()
        {
            return Name.Starts("Temp_");
        }

        public PropertyData(FProperty? prop, UObject owner) : this(new PropertyContainer(prop), owner) { }
        public PropertyData(UProperty? prop, UObject owner) : this(new PropertyContainer(prop), owner) { }

        public PropertyData(PropertyContainer? prop, UObject? owner)
        {
            if (prop == null || owner == null)
                return;

            Name = prop.GetName();
            Owner = owner.GetPathName();
            OwnerObject = owner;
            Flags = prop.GetFlags();
            MakePinType(prop);
            if (prop.New is FMulticastInlineDelegateProperty d)
            {
                DelegateSignatureFunction = d.SignatureFunction.Name;
                DelegateSignatureObjectPath = d.SignatureFunction.ResolvedObject?.Outer?.GetPathName() ??
                                              throw new DecompilerException("Delegate signature function object is null");
                DelegateSignatureObject = d.SignatureFunction.ResolvedObject.Outer.Load();
            }
            prop.Clear();
        }

        public PropertyData(
            string name,
            string owner,
            string type,
            EPropertyFlags flags,
            string className,
            string innerProp,
            string valueProp,
            string delegateSignatureFunction,
            string delegateSignatureObject)
        {
            Name = name;
            Owner = owner;
            Flags = flags;
            DelegateSignatureFunction = delegateSignatureFunction;
            DelegateSignatureObjectPath = delegateSignatureObject;
            MakePinType(type, innerProp, valueProp, className);
        }

        private string GetClassName(PropertyContainer prop)
        {
            FPackageIndex? index;
            if (prop.IsNew)
            {
                index = prop.New switch
                {
                    FInterfaceProperty p => p.InterfaceClass,
                    FSoftClassProperty p => p.MetaClass,
                    FClassProperty p => p.MetaClass,
                    FObjectProperty p => p.PropertyClass,
                    FStructProperty p => p.Struct,
                    FByteProperty p => p.Enum,
                    FEnumProperty p => p.Enum,
                    _ => null,
                };
            }
            else
            {
                index = prop.Old switch
                {
                    UInterfaceProperty p => p.InterfaceClass,
                    USoftClassProperty p => p.MetaClass,
                    UClassProperty p => p.MetaClass,
                    UObjectProperty p => p.PropertyClass,
                    UStructProperty p => p.Struct,
                    UByteProperty p => p.Enum,
                    UEnumProperty p => p.Enum,
                    _ => null,
                };
            }

            if (index == null || index.IsNull) { return ""; };
            PropertyClassPackageIndex = index;
            return index.ResolvedObject?.GetPathName() ?? "";
        }

        // Make pin type for this property
        private void MakePinType(PropertyContainer prop)
        {
            PinType = new GraphPinType();

            PropertyContainer propContainer;

            if (prop.IsNew)
            {
                propContainer = prop.New switch
                {
                    FMapProperty p => new PropertyContainer(p.KeyProp!),
                    FSetProperty p => new PropertyContainer(p.ElementProp!),
                    FArrayProperty p => new PropertyContainer(p.Inner!),
                    _ => prop,
                };

                PinType.ContainerType = prop.New switch
                {
                    FMapProperty => EPinContainerType.Map,
                    FSetProperty => EPinContainerType.Set,
                    FArrayProperty => EPinContainerType.Array,
                    _ => EPinContainerType.None,
                };

                if (prop.New is FMapProperty map)
                {
                    PinType.PinSubCategory = PropTypeToPinType(new PropertyContainer(map.ValueProp!).GetPropType());
                }
            }
            else
            {
                throw new NotImplementedException();
            }

            PinType.PinCategory = PropTypeToPinType(propContainer.GetPropType());
            PinType.PinSubCategoryObject = GetClassName(propContainer);
            PinType.IsReference = Flags.HasFlag(EPropertyFlags.OutParm) && Flags.HasFlag(EPropertyFlags.ReferenceParm);
        }

        // Make pin type from types string
        private void MakePinType(string type, string inner, string value, string className)
        {
            PinType = new GraphPinType();

            PinType.ContainerType = type switch
            {
                "MapProperty" => EPinContainerType.Map,
                "SetProperty" => EPinContainerType.Set,
                "ArrayProperty" => EPinContainerType.Array,
                _ => EPinContainerType.None,
            };

            PinType.PinCategory = PropTypeToPinType(inner == None || type == "EnumProperty" ? type : inner);
            if (PinType.ContainerType == EPinContainerType.Map)
                PinType.PinSubCategory = PropTypeToPinType(value);
            PinType.PinSubCategoryObject = className == None ? None : className;
            PinType.IsReference = Flags.HasFlag(EPropertyFlags.OutParm) && Flags.HasFlag(EPropertyFlags.ReferenceParm);
        }

        private static PinType PropTypeToPinType(string type)
        {
            if (type == None)
                return EngineBPData.PinType.Unknown;

            if (type == "StrProperty")
            {
                return EngineBPData.PinType.String;
            }
            if (type.Ends("DelegateProperty"))
            {
                return EngineBPData.PinType.Delegate;
            }
            if (type.Ends("Property"))
            {
                type = type[..^"Property".Length];
            }

            return Enum.GetValues<PinType>().FirstOrDefault(v => v.ToString() == type);
        }

    }
}
