using System;
using System.Reflection;

namespace Tbc.Target.Requests
{
    public record ProcessNewAssemblyRequest
    {
        public ProcessNewAssemblyRequest(Assembly assembly, Type? primaryType)
        {
            Assembly = assembly;
            PrimaryType = primaryType;
        }

        public Assembly Assembly { get; set; }
        public Type? PrimaryType { get; set; }
    }
}
