using System.Collections.Frozen;
using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;
using System.Linq;
using UEBlueprintGraphViewer.Engine;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_CallArrayFunction : K2Node_CallFunction
    {
        public K2Node_CallArrayFunction(FunctionData func, List<GraphPin> Parms, KismetExpression Instr, bool isPure) : base(func.Name, func.Outer.Name, Parms, Instr, isPure) { }

        protected override void CheckCustomLook(string funcName, string outerName, List<GraphPin> parms)
        {
            if (outerName is "/Script/Engine.KismetArrayLibrary" &&
                EngineBPData.KismetArrayLibrarySpecialNodes.TryGetValue(funcName, out var op))
                MakeShowNameAsBody(parms, op);

            // HACK: change array getter return type from int to actual array type
            if (funcName is "Array_Get" or "Array_Find" && parms.FirstOrDefault(o => o.IsOutput) is { } outPin)
                outPin.PinType = Utils.MakePinType(parms.FirstOrDefault(o => o.PinName == "TargetArray")?.PinType.PinCategory ?? EngineBPData.PinType.Unknown);
        }
    }
}
