using System.Collections.Frozen;
using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;
using System.Linq;
using UEBlueprintGraphViewer.Engine;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_CallFunction : BPNode
    {
        private string FunctionName;
        public K2Node_CallFunction(FunctionData func, string TargetName, List<GraphPin> Parms, KismetExpression Instr, bool isPure) : base(TargetName == "" ? func.Name : $"{func.Name} ({TargetName})", Instr)
        {
            FunctionName = func.Name;

            CheckCustomLook(func, Parms);
            
            MakePins(!isPure, !isPure, Parms);
            Pure = isPure;
        }

        protected virtual void CheckCustomLook(FunctionData func, List<GraphPin> parms)
        {
            switch (func.Outer.Name)
            {
                case "KismetMathLibrary" or "KismetStringLibrary" or "KismetSystemLibrary" or "KismetTextLibrary" or "EnhancedInputLibrary" when
                    func.Name.Starts("Conv_"):
                    MakeShowNameAsBody(parms, "", func.Name.Ends("ToText"));
                    break;
                case "KismetMathLibrary" when
                    EngineBPData.KismetMathLibrarySpecialNodes.TryGetValue(func.Name, out var op):
                    MakeShowNameAsBody(parms, op);
                    break;
                case "KismetStringLibrary" when
                    EngineBPData.KismetStringLibrarySpecialNodes.TryGetValue(func.Name, out var op):
                    MakeShowNameAsBody(parms, op);
                    break;
                case "KismetTextLibrary" when
                    EngineBPData.KismetTextLibrarySpecialNodes.TryGetValue(func.Name, out var op):
                    MakeShowNameAsBody(parms, op);
                    break;
                case "WidgetBlueprintLibrary" when
                    EngineBPData.WidgetBlueprintLibrarySpecialNodes.TryGetValue(func.Name, out var op):
                    MakeShowNameAsBody(parms, op);
                    break;
                case "BlueprintMapLibrary" when
                    EngineBPData.BlueprintMapLibrarySpecialNodes.TryGetValue(func.Name, out var op):
                    MakeShowNameAsBody(parms, op);
                    break;
                case "BlueprintSetLibrary" when
                    EngineBPData.BlueprintSetLibrarySpecialNodes.TryGetValue(func.Name, out var op):
                    MakeShowNameAsBody(parms, op);
                    break;
            }
        }
        
        protected void MakeShowNameAsBody(List<GraphPin> Parms, string op, bool hideFirstOnly = false)
        {
            HeaderHidden = true;
            ShowNameAsBody = true;
            if (hideFirstOnly)
            {
                Parms.FirstOrDefault()?.IsNameHidden = true;
                Parms.FirstOrDefault(o => o.IsOutput)?.IsNameHidden = true;
            }
            else
            {
                foreach (var parm in Parms)
                    parm.IsNameHidden = true;
            }
            Name = op;
            Pure = true;
        }

        public string GetFunctionName()
        {
            return FunctionName;
        }
    }
}
