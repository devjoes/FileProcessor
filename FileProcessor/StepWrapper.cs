using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileProcessor
{
    public class StepWrapper
    {
        private readonly CancellationToken cancel;

        public StepWrapper(CancellationToken cancel)
        {
            this.cancel = cancel;
        }

        public Task[] Tasks { get; set; }

        public AwaitableBlockingCollection<WorkWrapper<TIn>> Setup<TIn, TOut>(Func<TIn, IAsyncEnumerable<TOut>> step,
            StepOptions options, AwaitableBlockingCollection<WorkWrapper<object>> next,
            CancellationToken cancellationToken)
        {
            var buffer = new AwaitableBlockingCollection<WorkWrapper<TIn>>(options.BufferCapacity);
            //var enumerable = buffer.GetConsumingEnumerable(this.cancel);

            // This ensures each task gets its own thread and can run concurrently otherwise
            // we could deadlock/await a long time. The only alternative would have been
            // to use threads directly or delegate the call to mainLoop() and just chuck it on to the
            // stack without any way of managing its lifecycle/state using BeginInvoke
            // (which I don't think you can do in core anymore anyway)
            this.Tasks = Enumerable.Range(0, options.Parallelism)
                .Select(_ => Task.Factory.StartNew(async () => await this.mainLoop(buffer, next, step, cancellationToken),
                    CancellationToken.None, TaskCreationOptions.LongRunning, new ThreadPerTaskScheduler())).ToArray();
            return buffer;
        }
        //TODO: Add throw on error option
        //TODO: make it not pass null if no output from step
        private async Task mainLoop<TIn, TOut>(AwaitableBlockingCollection<WorkWrapper<TIn>> enumerable, AwaitableBlockingCollection<WorkWrapper<object>> next, Func<TIn, IAsyncEnumerable<TOut>> step, CancellationToken cancellationToken)
        {
            bool finished = false;
            //using var enumerator = enumerable.GetEnumerator();
            while (!finished && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumed = await enumerable.TakeAsync(cancellationToken);
                    finished = consumed == default;
                    Debug.WriteLine(consumed?.Index + "");
                    if (finished)
                    {
                        continue;
                    }
                    
                    try
                    {
                        if (consumed.CompletionSource.Task.IsCompleted)
                            next.Add(WorkWrapper<object>.NoOperation(consumed), this.cancel);
                        else
                            await this.passWorkToNextStep(next, step(consumed.Work), consumed);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                        Debug.WriteLine(ex.StackTrace);
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.StackTrace);
                        consumed.CompletionSource.SetException(ex);
                        next.Add(WorkWrapper<object>.NoOperation(consumed), this.cancel);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    Debug.WriteLine(ex.StackTrace);
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }
            }

            next.CompleteAdding();
            if (step is IAsyncDisposable ad) await ad.DisposeAsync();
            (step as IDisposable)?.Dispose();
        }

        private async Task passWorkToNextStep<TIn, TOut>(AwaitableBlockingCollection<WorkWrapper<object>> next,
            IAsyncEnumerable<TOut> enumerable,
            WorkWrapper<TIn> consumed)
        {
            var enumerator = enumerable.GetAsyncEnumerator(this.cancel);
            var finished = !await enumerator.MoveNextAsync();
            var first = true;
            var subTasks = new List<Task>();

            do
            {
                var result = enumerator.Current;
                finished |= !await enumerator.MoveNextAsync();
                if (finished && first)
                {
                    next.Add(new WorkWrapper<object>(result, consumed)
                    {
                        CompletionSource = consumed.CompletionSource
                    }, this.cancel);
                }
                else
                {
                    var individualCompletionSource = new TaskCompletionSource<object>();
                    subTasks.Add(individualCompletionSource.Task);
                    next.Add(new WorkWrapper<object>(result, consumed)
                    {
                        CompletionSource = individualCompletionSource
                    }, this.cancel);
                    if (subTasks.Count > 100) //TODO: Make this configurable
                    {
                        subTasks.RemoveAll(t => t.IsCompleted);
                    }
                }

                first = false;
            } while (!finished);

            if (subTasks.Any())
            {
                await Task.WhenAll(subTasks);
                consumed.CompletionSource.SetResult(null);
            }
        }

        public class FluentBuilder<TFirstIn, T>
        {
            private readonly Builder builder;

            public FluentBuilder(Builder builder)
            {
                this.builder = builder;
            }

            public FluentBuilder<TFirstIn, TOut> AddStep<TOut>(Func<T, TOut> step, StepOptions options = null)
            {
                this.builder.AddStep(step, options);
                return new FluentBuilder<TFirstIn, TOut>(this.builder);
            }

            public FluentBuilder<TFirstIn, TOut> AddStep<TOut>(Func<T, Task<TOut>> step, StepOptions options = null)
            {
                this.builder.AddStep(step, options);
                return new FluentBuilder<TFirstIn, TOut>(this.builder);
            }

            public FluentBuilder<TFirstIn, TOut> AddStep<TOut>(IStep<T, TOut> step, StepOptions options = null)
            {
                this.builder.AddStep(step, options);
                return new FluentBuilder<TFirstIn, TOut>(this.builder);
            }

            public FluentBuilder<TFirstIn, TOut> AddStep<TOut>(IAsyncStep<T, TOut> step, StepOptions options = null)
            {
                this.builder.AddStep(step, options);
                return new FluentBuilder<TFirstIn, TOut>(this.builder);
            }

            public FluentBuilder<TFirstIn, TOut> AddStep<TOut>(IAsyncEnumerableStep<T, TOut> step,
                StepOptions options = null)
            {
                this.builder.AddStep(step, options);
                return new FluentBuilder<TFirstIn, TOut>(this.builder);
            }

            public Func<TFirstIn, Task<TOut>> Returns<TOut>(CancellationToken cancel = default, bool autoDispose = true)
            {
                return this.builder.Build<TFirstIn, TOut>(cancel, autoDispose);
            }

            public Func<TFirstIn, IAsyncEnumerable<TOut>> ReturnsAsyncEnumerable<TOut>(
                CancellationToken cancel = default, bool autoDispose = true)
            {
                return this.builder.BuildEnumerable<TFirstIn, TOut>(cancel, autoDispose);
            }

            public FluentBuilder<TFirstIn, T> AfterCompletion(Func<Task> disposeStep)
            {
                this.builder.RunAfterCompletion.Add(disposeStep);
                return this;
            }
        }
    }
}