using CUE4Parse.UE4.Kismet;
using System.Collections.Generic;
using UEBlueprintGraphViewer.Engine;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_CallDelegate : BPNode
    {
        private string DelegateName;
        public K2Node_CallDelegate(DelegateData data, List<GraphPin> Parms, KismetExpression Instr) : base($"Call {data.Name}", Instr)
        {
            this.DelegateName = data.Name;
            MakePins(true, true, Parms, data.ContextInputPin);
        }

        public string GetDelegateName()
        {
            return DelegateName;
        }
    }
}
