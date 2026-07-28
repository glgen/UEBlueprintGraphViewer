using System;
using System.Collections.Generic;
using System.Linq;
using UEBlueprintGraphViewer.Engine;
using UEBlueprintGraphViewer.Nodes;

namespace UEBlueprintGraphViewer.Decompiler
{
    public class LocalVariablesStorage
    {
        List<LocalVar> LocalVariables = [];
        List<LocalVar> OutVariables = [];

        public int LocalsCount => LocalVariables.Count;
        public void Create(string name, GraphPin pin)
        {
            if (Find(name) is not {} v)
            {
                LocalVariables.Add(new LocalVar(name, pin));
            }
            else
            {
                v.ParamPin = pin;
            }
        }

        public void CreateOut(string name, GraphPin pin)
        {
            OutVariables.Add(new LocalVar(name, pin));
        }

        public void SetOut(string name, GraphPin pin)
        {
            int index = OutVariables.FindIndex(o => o.VarName.EqualsFName(name));
            OutVariables.RemoveAt(index);
            OutVariables.Insert(index, new LocalVar(name, pin));
        }

        public List<LocalVar> GetLocalVars()
        {
            return [.. LocalVariables];
        }
        
        public List<GraphPin> GetLocalPins()
        {
            return [.. LocalVariables.Select(o => o.ParamPin)];
        }

        public List<GraphPin> GetOutPins()
        {
            return [.. OutVariables.Select(o => o.ParamPin)];
        }

        public LocalVar? Find(string name)
        {
            return LocalVariables.Find(o => o.VarName.EqualsFName(name));
        }

        public bool TryFind(string name, out LocalVar? localVar)
        {
            localVar = Find(name);
            return localVar != null;
        }
        
        public LocalVar GetFromEnd(int index)
        {
            return LocalVariables[LocalVariables.Count - 1 - index];
        }
        public LocalVar? GetLastVarWithPrefix(string prefix)
        {
            return LocalVariables.Reverse<LocalVar>().FirstOrDefault(o => o.VarName.Starts(prefix));
        }

        public LocalVar? FindOut(string name)
        {
            return OutVariables.Find(o => o.VarName.EqualsFName(name));
        }

        public LocalVariablesStorage Clone()
        {
            LocalVariablesStorage storage = new();
            storage.LocalVariables.AddRange(LocalVariables);
            storage.OutVariables.AddRange(OutVariables);
            return storage;
        }
    }
}
