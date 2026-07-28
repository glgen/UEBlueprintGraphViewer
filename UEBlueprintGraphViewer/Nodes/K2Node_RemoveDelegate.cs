using CUE4Parse.UE4.Kismet;
using UEBlueprintGraphViewer.Engine;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_RemoveDelegate : BPNode
    {
        private string DelegateName;
        private GraphPin ContextPin;
        private GraphPin EventPin;
        public K2Node_RemoveDelegate(DelegateData data, KismetExpression Instr) : base($"Unbind Event from {data.Name}", Instr)
        {
            DelegateName = data.Name;
            ContextPin = data.ContextInputPin;
            EventPin = data.Delegate;
            EventPin.SetName("Event");
            MakePins(true, true);
        }

        public string GetDelegateName()
        {
            return DelegateName;
        }

        protected override void MakePins(bool needExec, bool needThen)
        {
            base.MakePins(needExec, needThen);
            AddInputPin(ContextPin);
            AddInputPin(EventPin);
        }
    }
}
