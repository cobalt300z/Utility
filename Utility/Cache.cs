using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Utility
{
    // Code taken from http://stackoverflow.com/questions/18367839/alternative-to-concurrentdictionary-for-portable-class-library
    // and https://gist.github.com/thomaslevesque/ecdd3640a0601768386f
    public class Cache<TKey, TValue>
    {
        private IImmutableDictionary<TKey, TValue> _cache = ImmutableDictionary.Create<TKey, TValue>();

        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            if (valueFactory == null) throw new ArgumentNullException("valueFactory");
            bool newValueCreated = false;
            TValue newValue = default(TValue);
            while (true)
            {
                var oldCache = _cache;
                TValue value;
                if (oldCache.TryGetValue(key, out value))
                    return value;

                //Value not found, create it if necessary
                if (!newValueCreated)
                {
                    newValue = valueFactory(key);
                    newValueCreated = true;
                }

                // Add the new value to the cache
                var newCache = oldCache.Add(key, newValue);

                // MSDN Documentation for CompareExchange: https://msdn.microsoft.com/en-us/library/bb297966(v=vs.110).aspx
                // if _cache = oldCache, newValue hasn't been commited yet and we have a clean copy of the cache, therefore replace _cache with newCache
                // else if _cache != oldCache, the newValue has either been commited or we have a dirty copy of the cache, therefore don't do anything else
                // NOTE: regardless of the outcome, the return value is ALWAYS the initial value passed in for _cache,
                // hence why the return value of CompareExchange will not always equal oldCache if the cache is dirty
                if (Interlocked.CompareExchange(ref _cache, newCache, oldCache) == oldCache)
                {
                    return newValue;
                }

                // Failed to write the value to the cache because of another thread
                // already changed it; try again.

            }
        }

        public void Clear()
        {
            _cache = _cache.Clear();
        }

        public bool ContainsValue(TValue value)
        {
            var temp = _cache.ToArray();

            for (int i = 0; i < temp.Length; i++)
            {
                if (temp[i].Value.Equals(value))
                    return true;
            }
            return false;
        }
    }
}
