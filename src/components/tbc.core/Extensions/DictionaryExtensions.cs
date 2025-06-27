using System.Collections.Generic;

namespace Tbc.Core.Extensions;

#if NET472
public static class DictionaryExtensions
{
    public static TValue? GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
        => !dict.TryGetValue(key, out var val) ? default : val;
}
#endif
