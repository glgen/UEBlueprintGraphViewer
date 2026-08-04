using System.Collections.Frozen;
using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.Utils;
using UEBlueprintGraphViewer.Engine;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_CallFunction : BPNode
    {
        public string FunctionName;
        public string OuterName;
        public K2Node_CallFunction(string funcName, string outerName, List<GraphPin> Parms, KismetExpression Instr, bool isPure) : base(outerName == "" ? funcName : $"{funcName} ({outerName.SubstringAfterLast('.')})", Instr)
        {
            FunctionName = funcName;
            OuterName = outerName;
            
            CheckCustomLook(funcName, outerName, Parms);
            
            MakePins(!isPure, !isPure, Parms);
            Pure = isPure;
        }

        protected virtual void CheckCustomLook(string funcName, string outerName, List<GraphPin> parms)
        {
            switch (outerName)
            {
                case "/Script/Engine.KismetMathLibrary" or "/Script/Engine.KismetStringLibrary" or "/Script/Engine.KismetSystemLibrary" or "/Script/Engine.KismetTextLibrary" or "/Script/EnhancedInput.EnhancedInputLibrary" when
                    funcName.Starts("Conv_"):
                    MakeShowNameAsBody(parms, "", funcName.Ends("ToText"));
                    break;
                case "/Script/Engine.KismetMathLibrary" when
                    EngineBPData.KismetMathLibrarySpecialNodes.TryGetValue(funcName, out var op):
                    MakeShowNameAsBody(parms, op);
                    break;
                case "/Script/Engine.KismetStringLibrary" when
                    EngineBPData.KismetStringLibrarySpecialNodes.TryGetValue(funcName, out var op):
                    MakeShowNameAsBody(parms, op);
                    break;
                case "/Script/Engine.KismetTextLibrary" when
                    EngineBPData.KismetTextLibrarySpecialNodes.TryGetValue(funcName, out var op):
                    MakeShowNameAsBody(parms, op);
                    break;
                case "/Script/UMG.WidgetBlueprintLibrary" when
                    EngineBPData.WidgetBlueprintLibrarySpecialNodes.TryGetValue(funcName, out var op):
                    MakeShowNameAsBody(parms, op);
                    break;
                case "/Script/Engine.BlueprintMapLibrary" when
                    EngineBPData.BlueprintMapLibrarySpecialNodes.TryGetValue(funcName, out var op):
                    MakeShowNameAsBody(parms, op);
                    break;
                case "/Script/Engine.BlueprintSetLibrary" when
                    EngineBPData.BlueprintSetLibrarySpecialNodes.TryGetValue(funcName, out var op):
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
