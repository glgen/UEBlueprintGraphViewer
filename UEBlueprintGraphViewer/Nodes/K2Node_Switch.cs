using CUE4Parse.UE4.Kismet;
using System;
using System.Collections.Generic;
using UEBlueprintGraphViewer.Decompiler;
using static UEBlueprintGraphViewer.Engine.EngineBPData;
using static UEBlueprintGraphViewer.Engine.Utils;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_Switch : BPNode
    {
        public K2Node_Switch(GraphPin Selection, List<KeyValuePair<string, BlockJump>> Cases, KismetExpression Instr) : base("Switch", Instr)
        {
            MakePins(true, false);

            Selection.SetName("Selection");

            AddInputPin(Selection);

            GraphPinType ExecPinType = MakePinType(PinType.exec);

            foreach (KeyValuePair<string, BlockJump> c in Cases)
            {
                GraphPin CaseExec = new GraphPin(c.Key, Engine.EngineEnums.EEdGraphPinDirection.EGPD_Output, ExecPinType);
                AddOutputPin(CaseExec);
            }
        }
    }
}
