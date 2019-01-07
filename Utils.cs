﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Lava.Net
{
    internal static class Utils
    {
        public static TSource MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector)
        {
            return source.MinBy(selector, null);
        }

        public static TSource MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector, IComparer<TKey> comparer)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (selector == null) throw new ArgumentNullException("selector");
            comparer = comparer ?? Comparer<TKey>.Default;

            using (var sourceIterator = source.GetEnumerator())
            {
                if (!sourceIterator.MoveNext())
                {
                    return default;
                }
                var min = sourceIterator.Current;
                var minKey = selector(min);
                while (sourceIterator.MoveNext())
                {
                    var candidate = sourceIterator.Current;
                    var candidateProjected = selector(candidate);
                    if (comparer.Compare(candidateProjected, minKey) < 0)
                    {
                        min = candidate;
                        minKey = candidateProjected;
                    }
                }
                return min;
            }
        }
        /*
        private static HttpClient Client = new HttpClient();
        public static async Task<Stream> GetStream(string url)
        {
            using (var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                if (response.IsSuccessStatusCode)
                {
                    using (var stream = response.Content.ReadAsStreamAsync())
                    {
                        return await stream;
                    }
                }
                else
                {
                    throw new HttpRequestException($"Response was not a success status code. ({(int)response.StatusCode})");
                }
            }
        }*/
        private static HttpClient Client = new HttpClient() { Timeout = Timeout.InfiniteTimeSpan };
        public static async Task<Stream> GetStream(string url)
        {
            try
            {
                
                var response = await Client.GetAsync(url,
                    HttpCompletionOption.ResponseHeadersRead);
                return await response.Content.ReadAsStreamAsync();
            }
            catch
            {
                Console.WriteLine("It's streaming, isn't it");
                throw;
            }
        }
    }
}
