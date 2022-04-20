using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tbc.Host.Components.IncrementalCompiler;

namespace Tbc.Host.Extensions
{
    public static class HelpfulExtensions
    {
        public static Task WhileTrue(Func<Task> @do) =>
            Task.Run(async () =>
            {
                while (true)
                    await @do();
            });
        
        public static JsonSerializerSettings NaiveCloneSerialiserSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None
        };

        public static T Clone<T>(this T obj)
            => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(obj, NaiveCloneSerialiserSettings), NaiveCloneSerialiserSettings);

        public static T CloneInto<T>(this object obj)
            => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(obj, NaiveCloneSerialiserSettings), NaiveCloneSerialiserSettings);

        public static T GetOrDefault<T, U>(this Dictionary<U, T> dict, U key)
            => dict.TryGetValue(key, out var val)
                ? val
                : default(T);

        public static string ToJson(this object o, bool indented = true) 
            => JsonConvert.SerializeObject(o, indented ? Formatting.Indented : Formatting.None);

        public static JObject ToJObject(this object o) 
            => JObject.FromObject(o);

        public static T Do<T>(this T obj, Action<T> action)
        {
            action(obj);
            return obj;
        }

        public static List<string> GetContainedTypes(this SyntaxTree syntaxTree)
        {
            var walker = new DeclarationSyntaxWalker<TypeDeclarationSyntax>();
            var classes = walker.Visit(syntaxTree).Select(c => c.Identifier.ToString()).ToList();

            return classes;
        }

        public static void LogInformationAsync(this ILogger logger, string message, params object[] args)
            => Task.Run(() => logger.LogInformation(message, args));
        
        public static HashSet<T> ToHashSet<T, U>(this IEnumerable<U> items, Func<U, T> selector)
            => new HashSet<T>(items.Select(selector));

        public static IEnumerable<T> DistinctBySelector<T, U>(this IEnumerable<T> items, Func<T, U> selector)
            => items.GroupBy(selector).Select(x => x.First());
    }
}
