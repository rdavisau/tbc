using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Tbc.Host.Extensions;

namespace Tbc.Host.Services.Patch.Models
{
    public class ProcessedCSharpReloadableInput : CSharpReloadableInput
    {
        public static ProcessedCSharpReloadableInput FromOriginal
            (CSharpReloadableInput sourceInput, Dictionary<string, string> namespaceReplacements, Dictionary<string, string> classReplacements)
        {
            var disambiguatedInput = sourceInput.CloneInto<ProcessedCSharpReloadableInput>();
            var disambiguatedInputSource = disambiguatedInput.Input.CloneInto<DisambiguatedCSharpClassSource>();

            var disambiguatedNamespace = namespaceReplacements.GetOrDefault(disambiguatedInputSource.OriginalNamespace) ?? disambiguatedInputSource.OriginalNamespace;
            var disambiguatedClassName = classReplacements.GetOrDefault(disambiguatedInputSource.OriginalClassName) ?? disambiguatedInputSource.OriginalClassName;

            var csb = sourceInput.Input.Source;
            var replacements = Enumerable.Concat(namespaceReplacements, classReplacements);
            var renames =
                replacements
                    .Select(x => (new Regex ("\\b" + x.Key + "\\b"), x.Value))
                    .ToList();

            Func<string, string> rename = c => {
                var rc = c;
                
                foreach (var r in renames) 
                    rc = r.Item1.Replace(rc, r.Item2);
                
                return rc;
            };

            csb = rename(csb);
            var disambiguatedSource = csb;

            foreach (var us in disambiguatedInput.Usings.ToList())
            {
                if (!namespaceReplacements.TryGetValue(us, out var replacement))
                    continue;

                var idx = disambiguatedInput.Usings.IndexOf(us);
                disambiguatedInput.Usings.RemoveAt(idx);
                disambiguatedInput.Usings.Insert(idx, replacement);
            }
            
            disambiguatedInputSource.Namespace = disambiguatedNamespace;
            disambiguatedInputSource.ClassName = disambiguatedClassName;
            disambiguatedInputSource.Source = disambiguatedSource;

            disambiguatedInput.Input = disambiguatedInputSource;
            disambiguatedInput.RawSource = sourceInput.Input;
            disambiguatedInput.RawInput = sourceInput;
            
            disambiguatedInput.Usings.Add(sourceInput.Input.OriginalNamespace);

            return disambiguatedInput;
        }

        public CSharpReloadableInput RawInput { get; set; }
        public CSharpClassSource RawSource { get; set; }
    }
}