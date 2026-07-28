using CUE4Parse.UE4.Kismet;
using UEBlueprintGraphViewer.ControlFlow;
using UEBlueprintGraphViewer.Nodes;

namespace UEBlueprintGraphViewer.Decompiler
{
    public class DecompilerContext
    {
        public FunctionDecompiler Decompiler;
        public GlobalDecompilerContext Global;

        public GameSettings Game;

        public GraphPin LastPin;

        public LocalVariablesStorage LocalVars;

        public ControlFlowContext ControlFlow;

        public InstrBlock Block;
        public int BlockIndex;
        
        public uint? EntryPoint;

        public ParamMappings ParamsDump { get => Game.ParamsDump; }

        public KismetExpression GetInstr()
        {
            return Block.Instructions[BlockIndex];
        }

        public KismetExpression GetInstrOffset(int offset)
        {
            return Block.Instructions[BlockIndex + offset];
        }

        public KismetExpression GetInstrInBlock(int index)
        {
            return Block.Instructions[index];
        }

        public void ResolveJump(BlockJump jmp)
        {
            BlockIndex = jmp.StartIndex;
            Block = jmp.Destination;
        }

        public void MarkAsParsed()
        {
            Global.MarkAsParsed(Block.Instructions[BlockIndex].StatementIndex);
        }

        public void MarkAsParsedOffset(int offset)
        {
            Global.MarkAsParsed(Block.Instructions[BlockIndex + offset].StatementIndex);
        }

        public void MarkAsParsed(InstrBlock block, int index)
        {
            Global.MarkAsParsed(block.Instructions[index].StatementIndex);
        }

        public void MarkAsParsedAndCanVisitAgain()
        {
            Global.MarkAsParsedAndCanVisitAgain(Block.Instructions[BlockIndex].StatementIndex);
        }

        public void AddNode(BPNode node)
        {
            Decompiler.Graph.AddNode(node);
        }

        public void ProcessBranch(DecompilerContext branch)
        {
            ProcessBranchChecked(branch);
        }

        public void ProcessBranch(BlockJump jump, GraphPin firstPin)
        {
            ProcessBranchChecked(MakeBranch(jump, firstPin));
        }

        public void ProcessBranch(InstrBlock block, int start, GraphPin firstPin)
        {
            ProcessBranchChecked(MakeBranch(block, start, firstPin));
        }

        public void ProcessBranchUnchecked(BlockJump jump, GraphPin firstPin)
        {
            Decompiler.ProcessInstructions(MakeBranch(jump, firstPin));
        }

        private void ProcessBranchChecked(DecompilerContext branch)
        {
            if (branch.ControlFlow.IsStartOfSequencePart(branch.Block, branch.BlockIndex))
                return;
            Decompiler.ProcessInstructions(branch);
        }

        public DecompilerContext MakeBranch(BlockJump jump, GraphPin firstPin)
        {
            DecompilerContext newContext = new DecompilerContext(Decompiler, Global, jump.Destination, jump.StartIndex, firstPin, LocalVars.Clone(), ControlFlow, EntryPoint);
            return newContext;
        }

        public DecompilerContext MakeBranch(InstrBlock block, int blockIndex, GraphPin firstPin)
        {
            DecompilerContext newContext = new DecompilerContext(Decompiler, Global, block, blockIndex, firstPin, LocalVars.Clone(), ControlFlow, EntryPoint);
            return newContext;
        }

        public DecompilerContext(FunctionDecompiler decompiler, GlobalDecompilerContext global, InstrBlock block, int blockIndex, GraphPin firstPin, LocalVariablesStorage localVars, ControlFlowContext controlFlow, uint? entryPoint)
        {
            Decompiler = decompiler;
            Global = global;
            Game = global.Game;
            LastPin = firstPin;
            LocalVars = localVars;
            ControlFlow = controlFlow;
            Block = block;
            BlockIndex = blockIndex;
            EntryPoint = entryPoint;
        }

        public DecompilerContext(FunctionDecompiler decompiler, GlobalDecompilerContext global, GraphPin firstPin, LocalVariablesStorage localVars, ControlFlowContext controlFlow, uint? entryPoint)
            : this(decompiler, global, controlFlow.Flow.Blocks[0], 0, firstPin, localVars, controlFlow, entryPoint) { }
    }
}
