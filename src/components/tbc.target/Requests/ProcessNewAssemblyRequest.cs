using System;
using System.Reflection;

namespace Tbc.Target.Requests
{
    public class ProcessNewAssemblyRequest
    {
        public Assembly Assembly { get; set; }
        public Type PrimaryType { get; set; }
    }
}