using CUE4Parse.UE4.Kismet;
using System;
using System.Collections.Generic;
using UEBlueprintGraphViewer.Engine;

namespace UEBlueprintGraphViewer.Nodes
{
    internal class K2Node_PromotableOperator : BPNode
    {
        public K2Node_PromotableOperator(string FuncName, List<GraphPin> Parms, KismetExpression Instr) : base(FuncName, Instr)
        {
            HeaderHidden = true;
            ShowNameAsBody = true;
            
            string opKey = FuncName.Split('_')[0];
            
            Name = EngineBPData.PromotableOperators.GetValueOrDefault(opKey) ??
                throw new Exception($"Unknown K2Node_PromotableOperator type {opKey}");

            foreach (GraphPin pin in Parms)
                pin.PinFriendlyName = "";

            MakePins(false, false, Parms);
        }
    }
}
