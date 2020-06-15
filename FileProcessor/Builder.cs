using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FileProcessor
{
    public class Builder
    {
        private Type lastOutType = null;

        public Builder()
        {
            this.Steps = new List<Func<object, IAsyncEnumerable<object>>>();
        }
        public StepWrapper.FluentBuilder<TIn, TOut> AddStep<TIn, TOut>(IAsyncStep<TIn, TOut> step)
        {
            return this.AddStep<TIn, TIn, TOut>((Func<TIn, Task<TOut>>)step.Execute);
        }
       
        public StepWrapper.FluentBuilder<TIn, TOut> AddStep<TIn, TOut>(IAsyncEnumerableStep<TIn, TOut> step)
        {
            return this.AddStep<TIn, TIn, TOut>((Func<TIn, IAsyncEnumerable<TOut>>)step.Execute);
        }
        public StepWrapper.FluentBuilder<TIn, TOut> AddStep<TIn, TOut>(IStep<TIn, TOut> step)
        {
            return this.AddStep<TIn, TIn, TOut>((Func<TIn, TOut>)step.Execute);
        }

        public StepWrapper.FluentBuilder<TIn, TOut> AddStep<TIn, TOut>(Func<TIn, TOut> step)
        {
            return this.AddStep<TIn, TIn, TOut>(step);
        }
        public StepWrapper.FluentBuilder<TIn, TOut> AddStep<TIn, TOut>(Func<TIn, Task<TOut>> step)
        {
            return this.AddStep<TIn, TIn, TOut>(step);
        }

        public StepWrapper.FluentBuilder<TFirstIn, TOut> AddStep<TFirstIn, TIn, TOut>(Func<TIn, IAsyncEnumerable<TOut>> step)
        {
            this.handleArguments(step);
            this.Steps.Add(input => toSingleAsyncEnumerable(step((TIn)input)));
            return new StepWrapper.FluentBuilder<TFirstIn, TOut>(this);
        }

        private static async IAsyncEnumerable<object> toSingleAsyncEnumerable<TOut>(Task<TOut> task)
        {
            yield return await task;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private static async IAsyncEnumerable<object> toSingleAsyncEnumerable<TOut>(TOut value)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            yield return value;
        }

        public StepWrapper.FluentBuilder<TFirstIn, TOut> AddStep<TFirstIn, TIn, TOut>(Func<TIn, Task<TOut>> step)
        {
            this.handleArguments(step);
            this.Steps.Add(input => toSingleAsyncEnumerable(step((TIn)input)));
            return new StepWrapper.FluentBuilder<TFirstIn, TOut>(this);
        }

        public StepWrapper.FluentBuilder<TFirstIn, TOut> AddStep<TFirstIn, TIn, TOut>(Func<TIn, TOut> step)
        {
            this.handleArguments(step);
            this.Steps.Add(input => toSingleAsyncEnumerable(step((TIn)input)));
            return new StepWrapper.FluentBuilder<TFirstIn, TOut>(this);
        }

        private void handleArguments<TIn, TOut>(Func<TIn, TOut> step)
        {
            var inType = step.GetType().GenericTypeArguments.First();
            var outType = step.GetType().GenericTypeArguments.Last();
            if (outType.IsGenericType && outType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var result = outType.GetProperty("Result");
                outType = result?.PropertyType;
                if (outType == null)
                {
                    throw new ArgumentException("Could not determine Result of " +
                                                step.GetType().GenericTypeArguments.Last());
                }
            }

            if (this.lastOutType != null && !inType.IsAssignableFrom(this.lastOutType))
            {
                throw new ArgumentException(
                    $"Input type {inType.FullName} cannot be assigned from previous output type {this.lastOutType.FullName}");
            }

            this.lastOutType = outType;
        }

        public IList<Func<object, IAsyncEnumerable<object>>> Steps { get; }


        public Action<TFirstIn, bool> Build<TFirstIn>(AwaitableBlockingCollection<WorkWrapper<object>> finalStepBuffer, CancellationToken cancel)
        {
            var nextStepBuffer = finalStepBuffer;
            var setupSteps = new StepWrapper[this.Steps.Count];

            for (int i = this.Steps.Count - 1; i >= 0; i--)
            {
                var step = new StepWrapper(cancel);
                nextStepBuffer = step.Setup(this.Steps[i], nextStepBuffer);
                setupSteps[i] = step;
            }

            var firstStepBuffer = nextStepBuffer;

            return (i, finished) =>
            {
                if (finished)
                {
                    firstStepBuffer.CompleteAdding();
                    return;
                }
                var tcs = new TaskCompletionSource<object>();
                var work = new WorkWrapper<object> { Work = i, CompletionSource = tcs };
                firstStepBuffer.Add(work, cancel);
            };
        }

        public Func<TFirstIn, Task<TOut>> Build<TFirstIn, TOut>(CancellationToken cancel = default)
        {
            var finalStepBuffer = new AwaitableBlockingCollection<WorkWrapper<object>>();
            var build = this.Build<TFirstIn>(finalStepBuffer, cancel);
            return input =>
            {
                build(input, false);
                build(default, true);
                return this.getSingleReturnValue<TOut>(finalStepBuffer, cancel);
            };
        }

        public Func<IEnumerable<TFirstIn>, IAsyncEnumerable<TOut>> BuildEnumerable<TFirstIn, TOut>(CancellationToken cancel = default)
        {
            var finalStepBuffer = new AwaitableBlockingCollection<WorkWrapper<object>>();
            var build = this.Build<TFirstIn>(finalStepBuffer, cancel);
            return input =>
            {
                foreach (var item in input)
                {
                    build(item, false);
                }

                build(default, true);

                return this.getEnumerableReturnValues<TOut>(finalStepBuffer, cancel);
            };
        }

        private async IAsyncEnumerable<TOut> getEnumerableReturnValues<TOut>(AwaitableBlockingCollection<WorkWrapper<object>> finalBuffer,
            [EnumeratorCancellation] CancellationToken cancel = default)
        {
            bool foundType = false, isTask = true, isEnumerable = false, isAsyncEnumerable = false;
            while (!finalBuffer.IsCompleted)
            {
                var item = await finalBuffer.TakeAsync(cancel);
                if (!foundType)
                {
                    var type = item.Work.GetType();
                    isTask = typeof(Task<>).IsAssignableFrom(type);
                    isAsyncEnumerable = typeof(IAsyncEnumerable<>).IsAssignableFrom(type);
                    isEnumerable = typeof(IEnumerable<>).IsAssignableFrom(type);
                    foundType = true;
                }

                switch (item.Work)
                {
                    case TOut inst:
                        yield return inst;
                        break;
                    case Task<TOut> task:
                        yield return await task;
                        break;
                    case IAsyncEnumerable<TOut> asyncEnum:
                    {
                        await foreach (var outItem in asyncEnum.WithCancellation(cancel))
                        {
                            yield return outItem;
                        }

                        break;
                    }
                    case IEnumerable<TOut> syncEnum:
                    {
                        foreach (var outItem in syncEnum)
                        {
                            yield return outItem;
                        }

                        break;
                    }
                    default:
                        throw new InvalidOperationException(
                            $"Type {item.Work.GetType().FullName} cannot be converted to " +
                            $"{typeof(Task<TOut>).FullName}, {typeof(IAsyncEnumerable<TOut>).FullName}, {typeof(IEnumerable<TOut>).FullName} or {typeof(TOut).FullName}");
                }
            }
        }

        private async Task<TOut> getSingleReturnValue<TOut>(AwaitableBlockingCollection<WorkWrapper<object>> finalBuffer, CancellationToken cancel)
        {
            var enumerable = finalBuffer.GetConsumingEnumerable(cancel);
            var result = enumerable.First();
            if (!result.CompletionSource.Task.IsCompleted)
            {
                result.CompletionSource.SetResult(result.Work);
            }

            return (TOut)await result.CompletionSource.Task;
        }
    }
}