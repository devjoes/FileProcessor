using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileProcessor
{
    public class AwaitableBlockingCollection<T>:IDisposable
    {
        private readonly SemaphoreSlim isEmptySemaphore;
        private readonly BlockingCollection<T> blockingCollection;

        public AwaitableBlockingCollection()
        {
            this.isEmptySemaphore = new SemaphoreSlim(0,1);
            this.blockingCollection = new BlockingCollection<T>();
        }
        public bool IsCompleted => this.blockingCollection.IsCompleted;


        public IEnumerable<T> GetConsumingEnumerable(CancellationToken cancel)
        {
            foreach (var item in this.blockingCollection.GetConsumingEnumerable(cancel))
            {
                yield return item;
            }
        }

        public bool TryTake(out T item)
        {
            return this.blockingCollection.TryTake(out item);
        }

        public async Task<T> TakeAsync(CancellationToken cancel)
        {
            T item;
            while (!this.TryTake(out item))
            {
                await this.isEmptySemaphore.WaitAsync(cancel);
            }

            return item;
        }

        public void Add(T item, in CancellationToken cancel)
        {
            if (this.isEmptySemaphore.CurrentCount == 0)
            {
                this.isEmptySemaphore.Release();
            }

            this.blockingCollection.Add(item, cancel);
        }

        public void CompleteAdding()
        {
            this.blockingCollection.CompleteAdding();
        }

        public void Dispose()
        {
            this.isEmptySemaphore.Dispose();
            this.blockingCollection.Dispose();
        }
    }
}
