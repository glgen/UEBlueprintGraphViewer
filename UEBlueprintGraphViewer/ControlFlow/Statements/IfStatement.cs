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
            ParseJumpExpr(Context.GetInstr(), out KismetExpression? boolExpr);

            GraphPin conditionPin = Context.Decompiler.ArgToPin(boolExpr!);

            K2Node_IfThenElse ifThenElse = new K2Node_IfThenElse(conditionPin, Context.GetInstr());
            Context.AddNode(ifThenElse);
            Connect(Context.LastPin, ifThenElse.ExecPin);

            // processing all branches
            Context.ProcessBranch(Context.Block.Jumps[0], ifThenElse.ExecOutPin);
            if (Context.Block.Jumps.Count > 1)
                Context.ProcessBranch(Context.Block.Jumps[1], ifThenElse.ExecElsePin);
        }
    }
}