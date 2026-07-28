using System.Collections.Generic;
using UEBlueprintGraphViewer.ControlFlow.Statements;
using UEBlueprintGraphViewer.Decompiler;

namespace UEBlueprintGraphViewer.ControlFlow
{
    // Control flow context of the function. Contains important control flow statements of the function.
    public class ControlFlowContext
    {
        readonly public List<Sequence> Sequences = [];
        readonly public List<SpawnStatement> SpawnNodes = [];
        readonly public ExecutionFlow Flow = new();

        // Checks if this instruction is start of any sequences parts
        public bool IsStartOfSequencePart(InstrBlock block, int index)
        {
            return Sequences.Exists(s => s.HaveThisBranchStart(block, index));
        }

        // Find spawn statement by temp variable name
        public SpawnStatement? FindSpawnStatement(string returnName)
        {
            return SpawnNodes.Find(s => s.IsThisObject(returnName));
        }
    }
}
