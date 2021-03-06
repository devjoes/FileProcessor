﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FileProcessor
{
    public class AwaitableBlockingCollection<T> : IDisposable
    {
        private readonly BlockingCollection<T> blockingCollection;
        private readonly SemaphoreSlim isEmptySemaphore;

        public AwaitableBlockingCollection(int boundedCapacity)
        {
            this.isEmptySemaphore = new SemaphoreSlim(0, 1);
            this.blockingCollection = new BlockingCollection<T>(boundedCapacity);
        }

        public AwaitableBlockingCollection()
        {
            this.isEmptySemaphore = new SemaphoreSlim(0, 1);
            this.blockingCollection = new BlockingCollection<T>(10);
        }

        public bool IsCompleted => this.blockingCollection.IsCompleted;

        public void Dispose()
        {
            this.isEmptySemaphore.Dispose();
            this.blockingCollection.Dispose();
        }


        public IEnumerable<T> GetConsumingEnumerable(CancellationToken cancel)
        {
            while (!this.blockingCollection.IsCompleted)
                if (this.blockingCollection.TryTake(out var item))
                {
                    yield return item;
                }
        }

        public bool TryTake(out T item)
        {
            var success = this.blockingCollection.TryTake(out item);
            return success;
        }

        public async Task<T> TakeAsync(CancellationToken cancel)
        {
            T item = default;
            while (!this.TryTake(out item) && !this.IsCompleted)
                await Task.Delay(100, cancel);
            await this.isEmptySemaphore.WaitAsync(TimeSpan.FromMilliseconds(20), cancel);

            if (item is WorkWrapper<T> workWrapper)
            {
                if (workWrapper.CompletionSource.Task.IsFaulted)
                {
                    throw workWrapper.CompletionSource.Task.Exception
                          ?? new AggregateException(new InvalidOperationException("Task faulted"));
                }
            }
            return item;
        }

        public void Add(T item, in CancellationToken cancel)
        {
            if (this.isEmptySemaphore.CurrentCount == 0)
            {
                try
                {
                    this.isEmptySemaphore.Release();
                }catch(SemaphoreFullException){}
            }

            this.blockingCollection.Add(item, cancel);
        }

        public void CompleteAdding()
        {
            this.blockingCollection.CompleteAdding();
        }
    }
}