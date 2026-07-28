using System;
using System.Collections.Generic;

namespace UEBlueprintGraphViewer.Decompiler
{
    public class DecompilationResult
    {
        public bool IsSuccessful = true;
        public bool HasCriticalErrors;
        public List<DecompilationProblem> Problems = [];

        public void AddProblem(string message, DecompilerContext? context, bool isCritical)
        {
            Problems.Add(new()
            {
                Message = message,
                Context = context,
                IsCritical = isCritical
            });
            IsSuccessful = false;
            HasCriticalErrors |= isCritical;
        }
    }

    public class DecompilationProblem
    {
        public bool IsCritical;

        public string Message = "";

        public DecompilerContext? Context;
    }
}
