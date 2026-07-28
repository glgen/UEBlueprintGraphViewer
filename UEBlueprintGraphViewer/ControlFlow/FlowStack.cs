using System.Collections.Generic;
using System.Linq;

namespace UEBlueprintGraphViewer.ControlFlow
{
    public class FlowStack
    {
        private readonly List<uint> Stack = [];

        public void Push(uint Flow)
        {
            Stack.Add(Flow);
        }

        public void PushRange(IEnumerable<uint> range)
        {
            Stack.AddRange(range);
        }

        public uint Pop()
        {
            if (Stack.Count > 0)
            {
                uint Flow = Stack.Last();
                Stack.RemoveAt(Stack.Count - 1);
                return Flow;
            }
            else
            {
                throw new System.Exception("Pop execution flow failed: stack is empty");
            }
        }

        public FlowStack Clone()
        {
            FlowStack stack = new();
            stack.Stack.AddRange(Stack);
            return stack;
        }
    }
}
