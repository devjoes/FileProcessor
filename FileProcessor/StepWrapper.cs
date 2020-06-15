using System;
using System.Collections.Generic;
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

        public AwaitableBlockingCollection<WorkWrapper<TIn>> Setup<TIn, TOut>(Func<TIn, IAsyncEnumerable<TOut>> step, AwaitableBlockingCollection<WorkWrapper<object>> next)
        {
            //TODO: support multiple concurrent tasks
            var buffer = new AwaitableBlockingCollection<WorkWrapper<TIn>>();
            Task.Run(async () =>
            {
                foreach (var consumed in buffer.GetConsumingEnumerable(this.cancel))
                {
                    try
                    {
                        if (consumed.CompletionSource.Task.IsCompleted)
                        {
                            next.Add(new WorkWrapper<object>
                            {
                                Work = default,
                                CompletionSource = consumed.CompletionSource
                            }, this.cancel);
                        }
                        else
                        {
                            var enumerable = step(consumed.Work);
                            await this.passWorkToNextStep(next, enumerable, consumed);
                        }
                    }
                    catch(Exception ex)
                    {
                        consumed.CompletionSource.SetException(ex);
                        next.Add(new WorkWrapper<object>
                        {
                            Work = default,
                            CompletionSource = consumed.CompletionSource
                        }, this.cancel);
                    }
                }
                next.CompleteAdding();
            }, CancellationToken.None);
            return buffer;
        }

        private async Task passWorkToNextStep<TIn, TOut>(AwaitableBlockingCollection<WorkWrapper<object>> next, IAsyncEnumerable<TOut> enumerable,
            WorkWrapper<TIn> consumed)
        {
            var enumerator = enumerable.GetAsyncEnumerator(this.cancel);
            bool finished = !await enumerator.MoveNextAsync();
            bool first = true;
            List<Task<object>> subTasks = new List<Task<object>>();
            
            do
            {
                var result = enumerator.Current;
                finished |= !await enumerator.MoveNextAsync();
                if (finished && first)
                {
                    next.Add(new WorkWrapper<object>
                    {
                        Work = result,
                        CompletionSource = consumed.CompletionSource
                    }, this.cancel);
                }
                else
                {
                    var individualCompletionSource = new TaskCompletionSource<object>();
                    subTasks.Add(individualCompletionSource.Task);
                    //individualCompletionSource.Task.ContinueWith((task, allFinished) =>
                    //{
                    //    if ((bool)allFinished&& subTasks.All(t => t.IsCompleted))
                    //    {
                    //        consumed.CompletionSource.TrySetResult(subTasks.Select(t => t.Result));
                    //    }
                    //}, finished);
                    next.Add(new WorkWrapper<object>
                    {
                        Work = result,
                        CompletionSource = individualCompletionSource
                    }, this.cancel);
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
            public FluentBuilder<TFirstIn, TOut> AddStep<TOut>(Func<T, TOut> step)
            {
                this.builder.AddStep(step);
                return new FluentBuilder<TFirstIn, TOut>(this.builder);
            }
            public FluentBuilder<TFirstIn, TOut> AddStep<TOut>(Func<T, Task<TOut>> step)
            {
                this.builder.AddStep(step);
                return new FluentBuilder<TFirstIn, TOut>(this.builder);
            }

            public FluentBuilder<TFirstIn, TOut> AddStep<TOut>(IAsyncStep<T, TOut> step)
            {
                this.builder.AddStep(step);
                return new FluentBuilder<TFirstIn, TOut>(this.builder);
            }
            public FluentBuilder<TFirstIn, TOut> AddStep<TOut>(IAsyncEnumerableStep<T, TOut> step)
            {
                this.builder.AddStep(step);
                return new FluentBuilder<TFirstIn, TOut>(this.builder);
            }
            
            public Func<TFirstIn, Task<TOut>> Returns<TOut>()
            {
                return this.builder.Build<TFirstIn, TOut>();
            }

            public Func<IEnumerable<TFirstIn>, IAsyncEnumerable<TOut>> ReturnsAsyncEnumerable<TOut>()
            {
                return this.builder.BuildEnumerable<TFirstIn, TOut>();
            }

        }
    }
}