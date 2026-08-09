using System.Collections.Generic;
using UEBlueprintGraphViewer.Decompiler;
using UEBlueprintGraphViewer.Nodes;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.ControlFlow.Statements
{
    public class SequenceStatement : ControlFlowStatement
    {
        public List<BlockJump> Points;
        public int SourceIndex;
        public int EndIndex;

        public SequenceStatement(DecompilerContext context, Sequence sequence) : base(context)
        {
            Points = sequence.GetPoints(context.BlockIndex);
        }

        public override void Decompile()
        {
            // skip the sequence at the start of the ubergraph
            // that jumps to return after all event logic is done
            if (Context.Block.Type == BlockType.JumpToEntryPoint)
            {
                foreach (var point in Points)
                    Context.ProcessBranchUnchecked(point, Context.LastPin);
                return;
            }
            
            int branchesCount = Points.Count;

            K2Node_ExecutionSequence node = new K2Node_ExecutionSequence(branchesCount, Context.GetInstr());
            Context.Decompiler.Graph.AddNode(node);
            Connect(Context.LastPin, node.ExecPin);

            for (int i = 1; i < branchesCount - 1; i++)
                Context.MarkAsParsedOffset(i);

            for (int i = 0; i < branchesCount; i++)
            {
                GraphPin branchPin = node.Output[i];

                var jmp = Points[^(i + 1)];

                Context.ProcessBranchUnchecked(jmp, branchPin);
            }
        }
    }
}
