using CUE4Parse.UE4.Kismet;

namespace UEBlueprintGraphViewer.Nodes
{
    public class K2Node_AssignmentStatement : BPNode
    {
        public GraphPin VariablePin;

        public GraphPin ValuePin;

        public K2Node_AssignmentStatement(GraphPin VariablePin, GraphPin ValuePin, KismetExpression Instr) : base("Assign", Instr)
        {
            this.VariablePin = VariablePin;
            this.ValuePin = ValuePin;
            MakePins(true, true);
        }

        protected override void MakePins(bool needExec, bool needThen)
        {
            base.MakePins(needExec, needThen);
            AddInputPin(VariablePin);
            AddInputPin(ValuePin);
        }
    }
}
