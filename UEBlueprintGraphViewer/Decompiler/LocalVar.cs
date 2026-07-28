using System;
using UEBlueprintGraphViewer.Nodes;
using static UEBlueprintGraphViewer.Engine.EngineEnums;

namespace UEBlueprintGraphViewer.Decompiler
{
    public class LocalVar
    {
        // variable name
        public string VarName;

        // value pin, output for local var and input for temp var
        public GraphPin ParamPin;

        // value pin is input pin and contains value directly
        public bool IsDirectValue => ParamPin.Direction == EEdGraphPinDirection.EGPD_Input;

        public LocalVar(string name, GraphPin paramPin)
        {
            VarName = name;
            ParamPin = paramPin;
        }

        public override string ToString()
        {
            return VarName;
        }
    }
}
