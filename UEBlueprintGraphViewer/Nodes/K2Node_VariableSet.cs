using CUE4Parse.UE4.Kismet;

namespace UEBlueprintGraphViewer.Nodes
{
    public class K2Node_VariableSet : BPNode
    {
        // Variable
        public GraphPin ValuePin;

        // Object to set this variable
        private GraphPin? ContextPin;
        public K2Node_VariableSet(GraphPin ValuePin, GraphPin? ContextPin, KismetExpression Instr) : base("SET", Instr)
        {
            HeaderCenter = true;
            this.ValuePin = ValuePin;
            this.ContextPin = ContextPin;
            MakePins(true, true);
        }

        protected override void MakePins(bool needExec, bool needThen)
        {
            base.MakePins(needExec, needThen);
            AddInputPin(ValuePin);
            if (ContextPin != null)
            {
                AddInputPin(ContextPin);
            }
        }
    }
}
