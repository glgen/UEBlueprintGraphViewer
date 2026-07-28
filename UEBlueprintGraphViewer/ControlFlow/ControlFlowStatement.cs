using UEBlueprintGraphViewer.Decompiler;

namespace UEBlueprintGraphViewer.ControlFlow
{
    public abstract class ControlFlowStatement
    {
        protected DecompilerContext Context;

        protected ControlFlowStatement(DecompilerContext context)
        {
            Context = context;
        }

        public abstract void Decompile();
    }
}
