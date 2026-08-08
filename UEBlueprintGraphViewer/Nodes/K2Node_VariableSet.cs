using CUE4Parse.UE4.Kismet;
using UEBlueprintGraphViewer.Engine;

namespace UEBlueprintGraphViewer.Nodes
{
    public class K2Node_VariableSet : BPNode
    {
        // Variable
        public GraphPin ValuePin;

        // Object to set this variable
        private GraphPin? ContextPin;
        
        public PropertyData Property;
        
        public K2Node_VariableSet(PropertyData data, GraphPin ValuePin, GraphPin? ContextPin, KismetExpression Instr) : base("SET", Instr)
        {
            Property = data;
            HeaderHidden = true;
            CompactTitle = true;
            TintPin = ValuePin;
            TintHeaderOnly = true;
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
