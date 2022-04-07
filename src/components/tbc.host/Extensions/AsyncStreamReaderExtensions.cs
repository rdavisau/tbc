using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;

namespace Tbc.Host.Extensions
{
    public static class AsyncStreamReaderExtensions
    {
        /// <summary>
        /// Advances the stream reader to the next element in the sequence, returning the result asynchronously.
        /// </summary>
        /// <typeparam name="T">The message type.</typeparam>
        /// <param name="streamReader">The stream reader.</param>
        /// <returns>
        /// Task containing the result of the operation: true if the reader was successfully advanced
        /// to the next element; false if the reader has passed the end of the sequence.
        /// </returns>
        public static Task<bool> MoveNext<T>(this IAsyncStreamReader<T> streamReader)
            where T : class
        {
            if (streamReader == null)
            {
                throw new ArgumentNullException(nameof(streamReader));
            }

            return streamReader.MoveNext(CancellationToken.None);
        }

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="streamReader"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async static IAsyncEnumerable<T> ReadAllAsync<T>(
            this IAsyncStreamReader<T> streamReader,
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            if (streamReader == null)
            {
                throw new System.ArgumentNullException(nameof(streamReader));
            }

            while (await streamReader.MoveNext(cancellationToken))
            {
                yield return streamReader.Current;
            }
        }

        public async static IAsyncEnumerable<TOut> ReadAllAsync<TIn, TOut>(
            this IAsyncStreamReader<TIn> streamReader, Func<TIn, TOut> selector,
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            if (streamReader == null)
            {
                throw new System.ArgumentNullException(nameof(streamReader));
            }

            while (await streamReader.MoveNext(cancellationToken))
            {
                yield return selector(streamReader.Current);
            }
        }

        public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> enumerable) =>
            new SynchronousAsyncEnumerable<T>(enumerable);

        private class SynchronousAsyncEnumerable<T> : IAsyncEnumerable<T>
        {
            private readonly IEnumerable<T> _enumerable;

            public SynchronousAsyncEnumerable(IEnumerable<T> enumerable) =>
                _enumerable = enumerable;

            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
                new SynchronousAsyncEnumerator<T>(_enumerable.GetEnumerator());
        }

        private class SynchronousAsyncEnumerator<T> : IAsyncEnumerator<T>
        {
            private readonly IEnumerator<T> _enumerator;

            public T Current => _enumerator.Current;

            public SynchronousAsyncEnumerator(IEnumerator<T> enumerator) =>
                _enumerator = enumerator;

            public ValueTask DisposeAsync() =>
                new ValueTask(Task.CompletedTask);

            public ValueTask<bool> MoveNextAsync() =>
                new ValueTask<bool>(Task.FromResult(_enumerator.MoveNext()));
        }

        public static async Task ForEachAsync<T>(this IAsyncEnumerable<T> enumerable, Action<T> action)
        {
            await foreach (var item in enumerable)
            {
                action(item);
            }
        }
    }
}
