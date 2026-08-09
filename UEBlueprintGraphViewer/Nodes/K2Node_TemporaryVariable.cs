using CUE4Parse.UE4.Kismet;
using CUE4Parse.Utils;
using System.Linq;
using UEBlueprintGraphViewer.Engine;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.EngineEnums;

namespace UEBlueprintGraphViewer.Nodes
{
    public class K2Node_TemporaryVariable : BPNode
    {
        private GraphPinType Type;
        public string VarName;
        public GraphPin VarPin;
        public K2Node_TemporaryVariable(PropertyData prop, KismetExpression? instr) : base($"Local {prop.PinType.PinCategory}{GetNamePostfix(prop.Name)}", instr)
        {
            Pure = true;
            VarName = prop.Name;
            Type = prop.PinType;
            MakePins(prop);
        }

        private static string GetNamePostfix(string varName)
        {
            string[] parts = varName.SubstringBefore("_Variable").Split('_');
            string comment = parts.Length > 2 ? string.Join(' ', parts[2..]) : "";
            return string.IsNullOrEmpty(comment) ? "" : $" ({comment})";
        }

        protected void MakePins(PropertyData prop)
        {
            VarPin = new GraphPin("Variable", EEdGraphPinDirection.EGPD_Output, Type);
            VarPin.Property = prop;
            AddOutputPin(VarPin);
        }
    }
}
