using System.Collections.Generic;

namespace Tbc.Core.Extensions;

#if NETSTANDARD2_1
#else
public static class DictionaryExtensions
{
    public static TValue? GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
        => !dict.TryGetValue(key, out var val) ? default : val;
}
#endif
