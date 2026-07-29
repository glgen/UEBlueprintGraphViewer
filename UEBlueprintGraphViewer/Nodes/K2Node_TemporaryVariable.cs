using CUE4Parse.UE4.Kismet;
using CUE4Parse.Utils;
using System.Linq;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.EngineEnums;

namespace UEBlueprintGraphViewer.Nodes
{
    public class K2Node_TemporaryVariable : BPNode
    {
        private GraphPinType Type;
        public GraphPin VarPin;
        public K2Node_TemporaryVariable(string varName, GraphPinType type, KismetExpression? instr) : base($"Local {type.PinCategory}{GetNamePostfix(varName)}", instr)
        {
            Pure = true;
            this.Type = type;
            MakePins();
        }

        private static string GetNamePostfix(string varName)
        {
            string[] parts = varName.SubstringBefore("_Variable").Split('_');
            string comment = parts.Length > 2 ? string.Join(' ', parts[2..]) : "";
            return string.IsNullOrEmpty(comment) ? "" : $" ({comment})";
        }

        protected void MakePins()
        {
            VarPin = new GraphPin("Variable", EEdGraphPinDirection.EGPD_Output, Type);
            AddOutputPin(VarPin);
        }
    }
}
