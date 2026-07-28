using CUE4Parse.UE4.Kismet;
using UEBlueprintGraphViewer.Engine;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_ClearDelegate : BPNode
    {
        private string DelegateName;
        public K2Node_ClearDelegate(DelegateData data, KismetExpression Instr) : base($"Unbind all Events from {data.Name}", Instr)
        {
            this.DelegateName = data.Name;
            MakePins(true, true, data.ContextInputPin);
        }

        public string GetDelegateName()
        {
            return DelegateName;
        }
    }
}
