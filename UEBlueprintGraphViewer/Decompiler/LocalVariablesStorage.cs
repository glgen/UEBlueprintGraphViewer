using System;
using System.Collections.Generic;
using System.Linq;
using UEBlueprintGraphViewer.Engine;
using UEBlueprintGraphViewer.Nodes;

namespace UEBlueprintGraphViewer.Decompiler
{
    public class LocalVariablesStorage
    {
        private readonly List<LocalVar> _localVariables = [];
        private readonly List<LocalVar> _outVariables = [];

        public int LocalsCount => _localVariables.Count;
        public void Create(string name, GraphPin pin)
        {
            if (Find(name) is not {} v)
            {
                _localVariables.Add(new LocalVar(name, pin));
            }
            else
            {
                v.ParamPin = pin;
            }
        }

        public void CreateOut(string name, GraphPin pin)
        {
            _outVariables.Add(new LocalVar(name, pin));
        }

        public void SetOut(string name, GraphPin pin)
        {
            int index = _outVariables.FindIndex(o => o.VarName.EqualsFName(name));
            _outVariables.RemoveAt(index);
            _outVariables.Insert(index, new LocalVar(name, pin));
        }

        public List<LocalVar> GetLocalVars()
        {
            return [.. _localVariables];
        }
        
        public List<GraphPin> GetLocalPins()
        {
            return [.. _localVariables.Select(o => o.ParamPin)];
        }

        public List<GraphPin> GetOutPins()
        {
            return [.. _outVariables.Select(o => o.ParamPin)];
        }

        public LocalVar? Find(string name)
        {
            return _localVariables.Find(o => o.VarName.EqualsFName(name));
        }

        public bool TryFind(string name, out LocalVar? localVar)
        {
            localVar = Find(name);
            return localVar != null;
        }
        
        public LocalVar GetFromEnd(int index)
        {
            return _localVariables[_localVariables.Count - 1 - index];
        }
        public LocalVar? GetLastVarWithPrefix(string prefix)
        {
            return _localVariables.Reverse<LocalVar>().FirstOrDefault(o => o.VarName.Starts(prefix));
        }

        public LocalVar? FindOut(string name)
        {
            return _outVariables.Find(o => o.VarName.EqualsFName(name));
        }

        public LocalVariablesStorage Clone()
        {
            LocalVariablesStorage storage = new();
            storage._localVariables.AddRange(_localVariables);
            storage._outVariables.AddRange(_outVariables);
            return storage;
        }
    }
}
