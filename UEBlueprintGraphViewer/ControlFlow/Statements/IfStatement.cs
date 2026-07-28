using CUE4Parse.UE4.Kismet;
using UEBlueprintGraphViewer.Decompiler;
using UEBlueprintGraphViewer.Nodes;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.ControlFlow.Statements
{
    public class IfStatement : ControlFlowStatement
    {
        public IfStatement(DecompilerContext context) : base(context) { }

        public override void Decompile()
        {
            ParseJumpExpr(Context.GetInstr(), out KismetExpression? BoolExpr);

            GraphPin ConditionPin = Context.Decompiler.ArgToPin(BoolExpr!);

            K2Node_IfThenElse IfThenElse = new K2Node_IfThenElse(ConditionPin, Context.GetInstr());
            Context.AddNode(IfThenElse);
            Connect(Context.LastPin, IfThenElse.ExecPin);

            // processing all branches
            Context.ProcessBranch(Context.Block.Jumps[0], IfThenElse.ExecOutPin);
            if (Context.Block.Jumps.Count > 1)
                Context.ProcessBranch(Context.Block.Jumps[1], IfThenElse.ExecElsePin);
        }
    }
}