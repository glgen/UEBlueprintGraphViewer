using CUE4Parse.UE4.Kismet;
using System;
using System.Collections.Generic;
using UEBlueprintGraphViewer.Nodes;
using static UEBlueprintGraphViewer.Engine.Utils;
using static UEBlueprintGraphViewer.Engine.PropertiesUtils;
using UEBlueprintGraphViewer.Decompiler;
using System.Linq;
using UEBlueprintGraphViewer.Engine;

namespace UEBlueprintGraphViewer.ControlFlow.Statements
{
    public class SwitchStatement : ControlFlowStatement
    {
        SwitchNodeType _nodeType;
        string _comparisonTempVar;
        EnumData? _enumData;

        private static List<string> _successVarsPrefixes = Enum.GetValues<SwitchNodeType>()
            .Select(o => $"K2Node_Switch{o}_CmpSuccess")
            .ToList();

        public SwitchStatement(string comparisonTempVar, SwitchNodeType nodeType, DecompilerContext context) : base(context)
        {
            _nodeType = nodeType;
            _comparisonTempVar = comparisonTempVar;
        }

        public override void Decompile()
        {
            KismetExpression instr = Context.GetInstr();
            GraphPin condition = GetSwitchConditionPin();
            if (_nodeType == SwitchNodeType.Enum)
                _enumData = Context.Jmap.TryFindEnum(condition.PinType.PinSubCategoryObject);

            // for some reason UE allows to compile switch with same case statements
            List<KeyValuePair<string, BlockJump?>> cases = [];
            var last = Context.ControlFlow.Flow.Blocks.Last();
            while (Context.Block != last && IsSwitchCondition(_comparisonTempVar))
            {
                BlockJump? jump = Context.Block.Type == BlockType.BranchEndIfNot ? null : Context.Block.Jumps[1];
                cases.Add(new(GetSwitchCaseName(), jump));

                Context.MarkAsParsed(); // condition
                Context.MarkAsParsedOffset(1); // conditional jump
                Context.BlockIndex = Context.Block.Jumps[0].StartIndex;
                Context.Block = Context.Block.Jumps[0].Destination;
            }

            if (_nodeType == SwitchNodeType.Enum)
            {
                if (Context.Block.Jumps.Count != 0 && Context.Block.Jumps[0].GetDestination() is not EX_Return)
                    throw new DecompilerException("Enum switch statement must return when comparison is failed", Context);
                Context.MarkAsParsed();
            }
            else
            {
                cases.Add(new("Default", new(Context.Block, Context.Block, (uint)Context.GetInstr().StatementIndex)));
            }

            K2Node_Switch node = _nodeType switch
            {
                SwitchNodeType.Integer => new K2Node_SwitchInteger(condition, cases, instr),
                SwitchNodeType.String => new K2Node_SwitchString(condition, cases, instr),
                SwitchNodeType.Name => new K2Node_SwitchName(condition, cases, instr),
                SwitchNodeType.Enum => new K2Node_SwitchEnum(condition, cases, _enumData, instr),
                _ => throw new DecompilerException($"Unknown switch node type {_nodeType}", Context),
            };

            Context.AddNode(node);
            Connect(Context.LastPin, node.ExecPin);

            for (int i = 0; i < cases.Count; i++)
                if (cases[i].Value != null)
                    Context.ProcessBranch(cases[i].Value!, node.Output[i]);
        }

        private GraphPin GetSwitchConditionPin()
        {
            return GetConditionCallMathPin(0);
        }

        private GraphPin GetConditionCallMathPin(int paramIndex)
        {
            EX_LetBool letBool = (Context.GetInstr() as EX_LetBool)!;
            EX_CallMath callMath = (letBool.Assignment as EX_CallMath)!;
            return Context.Decompiler.ArgToPin(callMath.Parameters[paramIndex]);
        }

        private string GetSwitchCaseName()
        {
            string name = GetConditionCallMathPin(1).Value;
            if (_enumData != null && _enumData.Elements.FirstOrDefault(o => o.Value == Convert.ToInt64(name)).Key is { } enumElemName)
                name = enumElemName;
            return name;
        }

        // check if expression set comparison value to this statement comparison var
        private bool IsSwitchCondition(string cmp)
        {
            KismetExpression ex = Context.GetInstr();
            return CheckSwitchInstrs(ex) && cmp == VarInstrToName((ex as EX_LetBool)!.Variable);
        }

        // Check for switch branch instructions (if result of EX_CallMath set to bool temp var)
        private static bool CheckSwitchInstrs(KismetExpression expr)
        {
            return expr is EX_LetBool letBool &&
                letBool.Variable is EX_LocalVariable &&
                letBool.Assignment is EX_CallMath;
        }

        // Check if expression is first expression of switch node, returns comparison var, switch node type
        public static bool IsSwitchNodeFirstInstr(KismetExpression expr, out string CmpName, out SwitchNodeType Type)
        {
            Type = SwitchNodeType.Integer;
            CmpName = "";

            if (!CheckSwitchInstrs(expr))
                return false;

            string name = VarInstrToName((expr as EX_LetBool).Variable);
            CmpName = name;

            int index = _successVarsPrefixes.FindIndex(name.Starts);
            if (index != -1)
                Type = (SwitchNodeType)index;

            return index != -1;
        }
        
        
        public static bool CheckAndDecompile(KismetExpression expr, DecompilerContext context)
        {
            if (IsSwitchNodeFirstInstr(expr, out string comparisonTempVar, out SwitchNodeType nodeType))
            {
                new SwitchStatement(comparisonTempVar, nodeType, context).Decompile();
                return true;
            }
            return false;
        }
    }
}
