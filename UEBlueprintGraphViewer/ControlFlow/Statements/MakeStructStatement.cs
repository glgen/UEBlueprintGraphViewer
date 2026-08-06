using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;
using CUE4Parse.Utils;
using UEBlueprintGraphViewer.Decompiler;
using UEBlueprintGraphViewer.Engine;
using UEBlueprintGraphViewer.Nodes;
using static UEBlueprintGraphViewer.Engine.PropertiesUtils;

namespace UEBlueprintGraphViewer.ControlFlow.Statements
{
    public class MakeStructStatement(DecompilerContext context, KismetExpression firstInstr, EX_StructMemberContext structContext)
        : ControlFlowStatement(context)
    {
        KismetExpression firstInstr = firstInstr;
        EX_StructMemberContext structContext = structContext;

        public override void Decompile()
        {
            PropertyData firstStructVar = VarInstrToProperty(structContext.StructExpression, Context.Global);
            string StructVarName = firstStructVar.Name;
            string StructName = firstStructVar.PinType.PinSubCategoryObject.SubstringAfterLast('.');

            List<GraphPin> StructMembersPin = [];

            while (IsThisMakeStructSetter(StructVarName, out string PropName, out KismetExpression? Assignment))
            {
                GraphPin memberPin = Context.Decompiler.ArgToPin(Assignment!, PropName);
                StructMembersPin.Add(memberPin);
                Context.MarkAsParsed();
                Context.BlockIndex++;
            }

            K2Node_MakeStruct Node = new K2Node_MakeStruct(StructName, StructMembersPin, firstInstr);
            Context.AddNode(Node);
            Context.LocalVars.Create(StructVarName, Node.GetFirstOutputParam()!);
        }
        
        public static bool CheckAndDecompile(KismetExpression expr, DecompilerContext context)
        {
            if (!ParseStructSetter(expr, out _, out _, out _, out EX_StructMemberContext? structContext)) return false;
            new MakeStructStatement(context, expr, structContext!).Decompile();
            return true;
        }

        private bool IsThisMakeStructSetter(string structVarName, out string propName, out KismetExpression? assignment)
        {
            return ParseStructSetter(Context.GetInstr(), out string structName, out propName, out assignment, out _) &&
                structName == structVarName;
        }

        private static bool ParseStructSetter(KismetExpression ex, out string structVarName, out string propName, out KismetExpression? assignment, out EX_StructMemberContext? structContext)
        {
            structVarName = "";
            propName = "";
            assignment = null;
            structContext = null;
            
            switch (ex)
            {
                case EX_LetBase { Variable: EX_StructMemberContext context1, Assignment: { } assignment1 }:
                    structContext = context1;
                    assignment = assignment1;
                    break;
                case EX_Let { Variable: EX_StructMemberContext context2, Assignment: { } assignment2 }:
                    structContext = context2;
                    assignment = assignment2;
                    break;
                default:
                    return false;
            }

            if (structContext.StructExpression is not EX_VariableBase)
                return false;

            propName = Utils.StructMemberNameToFriendlyName(ToName(structContext.Property));
            structVarName = VarInstrToName(structContext.StructExpression);
            return structVarName.Starts("K2Node_MakeStruct_");
        }
    }
}
