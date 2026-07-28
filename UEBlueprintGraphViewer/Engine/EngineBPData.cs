using System.Collections.Frozen;
using System.Collections.Generic;
using UEBlueprintGraphViewer.Nodes;
using static UEBlueprintGraphViewer.Engine.EngineEnums;

namespace UEBlueprintGraphViewer.Engine
{
    public static class EngineBPData
    {
        public const string None = "None";

        public enum PinType : byte
        {
            Unknown,
            exec,
            Bool,
            Byte,
            Enum,
            Int,
            Int64,
            Float,
            Double,
            Name,
            String,
            Text,
            Vector,
            Rotator,
            Transform,
            Interface,
            Object,
            SoftObject,
            Class,
            SoftClass,
            Delegate,
            Struct,
            Wildcard,
        }

        public struct GraphPinType
        {
            public PinType PinCategory { get; set; }
            public PinType PinSubCategory { get; set; }
            public string PinSubCategoryObject;
            public EPinContainerType ContainerType { get; set; }
            public bool IsReference { get; set; }
        }

        public static readonly FrozenDictionary<string, string> PromotableOperators = new Dictionary<string, string>()
        {
            { "Add", "+" },
            { "Divide", "/" },
            { "EqualEqual", "==" },
            { "Greater", ">" },
            { "GreaterEqual", ">=" },
            { "Less", "<" },
            { "LessEqual", "<=" },
            { "Multiply", "*" },
            { "NotEqual", "!=" },
            { "Subtract", "-" },
        }.ToFrozenDictionary();
        
        public static readonly FrozenDictionary<string, string> KismetMathLibrarySpecialNodes = new Dictionary<string, string>()
        {
            ["BooleanAND"] = "AND",
            ["BooleanNAND"] = "NAND",
            ["BooleanNOR"] = "NOR",
            ["BooleanOR"] = "OR",
            ["BooleanXOR"] = "XOR",
            ["Not_PreBool"] = "NOT",
            ["Min"] = "MIN",
            ["Max"] = "MAX",
            ["BMin"] = "MIN",
            ["BMax"] = "MAX",
            ["FMin"] = "MIN",
            ["FMax"] = "MAX",
            ["MinInt64"] = "MIN",
            ["MaxInt64"] = "MAX",
            ["Abs"] = "ABS",
            ["Abs_Int"] = "ABS",
            ["Abs_Int64"] = "ABS",
            ["Sin"] = "SIN",
            ["Asin"] = "ASIN",
            ["Cos"] = "COS",
            ["Acos"] = "ACOS",
            ["Tan"] = "TAN",
            ["Exp"] = "e",
            ["Sqrt"] = "SQRT",
            ["Square"] = "^2",
            ["GetPI"] = "PI",
            ["GetTAU"] = "TAU",
            ["DegreesToRadians"] = "D2R",
            ["RadiansToDegrees"] = "R2D",
            ["DegSin"] = "SINd",
            ["DegAsin"] = "ASINd",
            ["DegCos"] = "COSd",
            ["DegAcos"] = "ACOSd",
            ["DegTan"] = "TANd",
            ["EqualExactly_Vector2DVector2D"] = "===",
            ["NotEqualExactly_Vector2DVector2D"] = "!==",
            ["EqualExactly_VectorVector"] = "===",
            ["NotEqualExactly_VectorVector"] = "!==",
            ["Dot_VectorVector"] = "dot",
            ["Cross_VectorVector"] = "cross",
            ["Percent_ByteByte"] = "%",
            ["Percent_IntInt"] = "%",
            ["Percent_Int64Int64"] = "%",
            ["Percent_FloatFloat"] = "%",
            ["And_IntInt"] = "&",
            ["Xor_IntInt"] = "^",
            ["Or_IntInt"] = "|",
            ["Not_Int"] = "~",
            ["And_Int64Int64"] = "&",
            ["Xor_Int64Int64"] = "^",
            ["Or_Int64Int64"] = "|",
            ["Not_Int64"] = "~",
            ["ComposeTransforms"] = "*",
        }.ToFrozenDictionary();
        
        public static readonly FrozenDictionary<string, string> KismetStringLibrarySpecialNodes = new Dictionary<string, string>()
        {
            ["EqualEqual_StrStr"] = "===",
            ["EqualEqual_StriStri"] = "==",
            ["NotEqual_StrStr"] = "!==",
            ["NotEqual_StriStri"] = "!=",
        }.ToFrozenDictionary();
        
        public static readonly FrozenDictionary<string, string> KismetTextLibrarySpecialNodes = new Dictionary<string, string>()
        {
            ["EqualEqual_TextText"] = "===",
            ["EqualEqual_IgnoreCase_TextText"] = "==",
            ["NotEqual_TextText"] = "!==",
            ["NotEqual_IgnoreCase_TextText"] = "!=",
        }.ToFrozenDictionary();
        
        public static readonly FrozenDictionary<string, string> WidgetBlueprintLibrarySpecialNodes = new Dictionary<string, string>()
        {
            ["GetInputEventFromKeyEvent"] = "",
            ["GetKeyEventFromAnalogInputEvent"] = "",
            ["GetInputEventFromCharacterEvent"] = "",
            ["GetInputEventFromPointerEvent"] = "",
            ["GetInputEventFromNavigationEvent"] = "",
        }.ToFrozenDictionary();
        
        public static readonly FrozenDictionary<string, string> KismetArrayLibrarySpecialNodes = new Dictionary<string, string>()
        {
            ["Array_Add"] = "ADD",
            ["Array_AddUnique"] = "ADDUNIQUE",
            ["Array_Shuffle"] = "SHUFFLE",
            ["Array_ShuffleFromStream"] = "SHUFFLE",
            ["Array_Identical"] = "==",
            ["Array_Append"] = "APPEND",
            ["Array_Insert"] = "INSERT",
            ["Array_Remove"] = "REMOVE INDEX",
            ["Array_RemoveItem"] = "REMOVE",
            ["Array_Clear"] = "CLEAR",
            ["Array_Resize"] = "RESIZE",
            ["Array_Reverse"] = "REVERSE",
            ["Array_Length"] = "LENGTH",
            ["Array_IsEmpty"] = "IS EMPTY",
            ["Array_IsNotEmpty"] = "IS NOT EMPTY",
            ["Array_LastIndex"] = "LAST INDEX",
            ["Array_Get"] = "GET",
            ["Array_Swap"] = "SWAP",
            ["Array_Find"] = "FIND",
            ["Array_Contains"] = "CONTAINS",
            ["Array_IsValidIndex"] = "IS VALID INDEX",
            ["Array_Random"] = "RANDOM",
        }.ToFrozenDictionary();

        public static readonly FrozenDictionary<string, string> BlueprintMapLibrarySpecialNodes = new Dictionary<string, string>()
        {
            ["Map_Add"] = "ADD",
            ["Map_Remove"] = "REMOVE",
            ["Map_Find"] = "FIND",
            ["Map_Contains"] = "CONTAINS",
            ["Map_Keys"] = "KEYS",
            ["Map_Values"] = "VALUES",
            ["Map_Length"] = "LENGTH",
            ["Map_IsEmpty"] = "IS EMPTY",
            ["Map_IsNotEmpty"] = "IS NOT EMPTY",
            ["Map_Clear"] = "CLEAR",
        }.ToFrozenDictionary();
        
        public static readonly FrozenDictionary<string, string> BlueprintSetLibrarySpecialNodes = new Dictionary<string, string>()
        {
            ["Set_Add"] = "ADD",
            ["Set_AddItems"] = "ADD ITEMS",
            ["Set_Remove"] = "REMOVE",
            ["Set_IsEmpty"] = "IS EMPTY",
            ["Set_IsNotEmpty"] = "IS NOT EMPTY",
            ["Set_RemoveItems"] = "REMOVE ITEMS",
            ["Set_ToArray"] = "TO ARRAY",
            ["Set_Clear"] = "CLEAR",
            ["Set_Length"] = "LENGTH",
            ["Set_Contains"] = "CONTAINS",
            ["Set_Intersection"] = "INTERSECTION",
            ["Set_Union"] = "UNION",
            ["Set_Difference"] = "DIFFERENCE",
        }.ToFrozenDictionary();
        
        public const string ActorSpawnFunctionName = "BeginDeferredActorSpawnFromClass";
        public const string ObjectSpawnFunctionName = "SpawnObject";
        public const string ActorAndObjectSpawnFunctionOuter = "GameplayStatics";

        public const string WidgetSpawnFunctionName = "Create";
        public const string WidgetSpawnFunctionOuter = "WidgetBlueprintLibrary";
    }
    
    public enum SwitchNodeType
    {
        Integer,
        String,
        Name,
        Enum,
    }

    public struct DelegateData
    {
        public GraphPin ContextInputPin;
        public GraphPin Delegate;
        public string Name;
        public string Owner;
    }

    public enum SpawnNodeType
    {
        None,
        Actor,
        Obj,
        Widget,
    }

    public struct InputEventData
    {
        public string Name;
        public string FunctionName;
        public InputEventPinType PinType;
        public InputEventType Type;
    }

    public struct TimelineData
    {
        public List<FloatTrack> FloatTracks;
        public string TimelineGuid;
        public string VariableName;
        public string DirectionPropertyName;
        public string UpdateFunctionName;
        public string FinishedFunctionName;
    }
    public struct FloatTrack
    {
        //public string CurveFloat;
        public string PropertyName;
        public string TrackName;
        public bool IsExternalCurve;
    }

    public enum InputEventType
    {
        Key,
        InputAxisKey,
        InputAction,
        InputAxisAction,
        EnhancedInputAction,
    }
        
    public enum InputEventPinType
    {
        Pressed,
        Released,
        Triggered,
        Started,
        Ongoing,
        Canceled,
        Completed,
        AxisExec,
    }
}
