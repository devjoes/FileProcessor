using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FileProcessor
{
    public class Builder : IDisposable
    {
        private Type lastOutType;

        public Builder()
        {
            this.Steps = new List<Func<object, IAsyncEnumerable<object>>>();
            this.RunAfterCompletion = new List<Func<Task>>();
            this.StepOptions = new List<StepOptions>();
        }

        public IList<Func<object, IAsyncEnumerable<object>>> Steps { get; }
        public IList<StepOptions> StepOptions { get; }
        public IList<Func<Task>> RunAfterCompletion { get; }

        public void Dispose()
        {
            Task.WaitAll(this.RunAfterCompletion.Select(i => i()).ToArray());
        }

        private static Func<Task> tryDispose(object step)
        {
            return async () =>
            {
                (step as IDisposable)?.Dispose();
                if (step is IAsyncDisposable ad) await ad.DisposeAsync();
            };
        }

        public StepWrapper.FluentBuilder<TIn, TOut> AddStep<TIn, TOut>(IAsyncEnumerableStep<TIn, TOut> step,
            StepOptions options = null)
        {
            return this
                .AddStep<TIn, TIn, TOut>((Func<TIn, IAsyncEnumerable<TOut>>) step.Execute, options ?? new StepOptions())
                .AfterCompletion(tryDispose(step));
        }

        public StepWrapper.FluentBuilder<TIn, TOut> AddStep<TIn, TOut>(IAsyncStep<TIn, TOut> step,
            StepOptions options = null)
        {
            return this
                .AddStep<TIn, TIn, TOut>((Func<TIn, Task<TOut>>) step.Execute, options ?? new StepOptions())
                .AfterCompletion(tryDispose(step));
        }

        public StepWrapper.FluentBuilder<TIn, TOut> AddStep<TIn, TOut>(IStep<TIn, TOut> step,
            StepOptions options = null)
        {
            return this
                .AddStep<TIn, TIn, TOut>((Func<TIn, TOut>) step.Execute, options ?? new StepOptions())
                .AfterCompletion(tryDispose(step));
        }

        public StepWrapper.FluentBuilder<TIn, TOut> AddStep<TIn, TOut>(Func<TIn, TOut> step, StepOptions options = null)
        {
            return this.AddStep<TIn, TIn, TOut>(step, options ?? new StepOptions());
        }

        public StepWrapper.FluentBuilder<TIn, TOut> AddStep<TIn, TOut>(Func<TIn, Task<TOut>> step,
            StepOptions options = null)
        {
            return this.AddStep<TIn, TIn, TOut>(step, options ?? new StepOptions());
        }

        public StepWrapper.FluentBuilder<TFirstIn, TOut> AddStep<TFirstIn, TIn, TOut>(
            Func<TIn, IAsyncEnumerable<TOut>> step, StepOptions options = null)
        {
            this.handleArguments(step);
            this.StepOptions.Add(options ?? new StepOptions());
            this.Steps.Add(input => enumerateAsyncEnumerable(step((TIn) input)));
            return new StepWrapper.FluentBuilder<TFirstIn, TOut>(this);
        }

        public StepWrapper.FluentBuilder<TFirstIn, TOut> AddStep<TFirstIn, TIn, TOut>(Func<TIn, Task<TOut>> step,
            StepOptions options = null)
        {
            this.handleArguments(step);
            this.StepOptions.Add(options ?? new StepOptions());
            this.Steps.Add(input =>
            {
                var output = step((TIn) input);
                return toSingleAsyncEnumerable(output);
            });
            return new StepWrapper.FluentBuilder<TFirstIn, TOut>(this);
        }

        public StepWrapper.FluentBuilder<TFirstIn, TOut> AddStep<TFirstIn, TIn, TOut>(Func<TIn, TOut> step,
            StepOptions options = null)
        {
            this.handleArguments(step);
            this.StepOptions.Add(options ?? new StepOptions());
            this.Steps.Add(input =>
            {
                var output = step((TIn) input);
                return toSingleAsyncEnumerable(output);
            });
            return new StepWrapper.FluentBuilder<TFirstIn, TOut>(this);
        }

        public StepWrapper.FluentBuilder<IEnumerable<T>, T> AcceptCollection<T>()
        {
            this.StepOptions.Insert(0, new StepOptions());
            this.Steps.Insert(0, input => toAsyncEnumerable((IEnumerable<T>) input));
            return new StepWrapper.FluentBuilder<IEnumerable<T>, T>(this);
        }

        private static async IAsyncEnumerable<object> enumerateAsyncEnumerable<TOut>(IAsyncEnumerable<TOut> enumerable)
        {
            await foreach (var item in enumerable) yield return item;
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

#pragma warning disable 1998
        private static async IAsyncEnumerable<object> toAsyncEnumerable<T>(IEnumerable<T> input)
#pragma warning restore 1998
        {
            foreach (var item in input) yield return item;
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
                    throw new ArgumentException("Could not determine Result of " +
                                                step.GetType().GenericTypeArguments.Last());
            }

            if (outType.IsGenericType && outType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
                outType = outType.GenericTypeArguments.Single();

            if (this.lastOutType != null && !inType.IsAssignableFrom(this.lastOutType))
                throw new ArgumentException(
                    $"Input type {inType.FullName} cannot be assigned from previous output type {this.lastOutType.FullName}");

            this.lastOutType = outType;
        }


        public Action<TFirstIn, bool> Build<TFirstIn>(AwaitableBlockingCollection<WorkWrapper<object>> finalStepBuffer,
            CancellationToken cancel)
        {
            var nextStepBuffer = finalStepBuffer;
            var setupSteps = new StepWrapper[this.Steps.Count];

            for (var i = this.Steps.Count - 1; i >= 0; i--)
            {
                var step = new StepWrapper(cancel);
                nextStepBuffer = step.Setup(this.Steps[i], this.StepOptions[i], nextStepBuffer, cancel);
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

                // Avoids deadlock https://devblogs.microsoft.com/premier-developer/the-danger-of-taskcompletionsourcet-class/
                var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                var work = new WorkWrapper<object> {Work = i, CompletionSource = tcs};
                firstStepBuffer.Add(work, cancel);
            };
        }

        public Func<TFirstIn, Task<TOut>> Build<TFirstIn, TOut>(CancellationToken cancel, bool autoDispose)
        {
            var finalStepBuffer = new AwaitableBlockingCollection<WorkWrapper<object>>();
            var build = this.Build<TFirstIn>(finalStepBuffer, cancel);
            return async input =>
            {
                build(input, false);
                build(default, true);
                return await this.getSingleReturnValue<TOut>(finalStepBuffer, cancel, autoDispose);
            };
        }

        public Func<TFirstIn, IAsyncEnumerable<TOut>> BuildEnumerable<TFirstIn, TOut>(CancellationToken cancel, bool autoDispose)
        {
            var finalStepBuffer = new AwaitableBlockingCollection<WorkWrapper<object>>();
            var build = this.Build<TFirstIn>(finalStepBuffer, cancel);
            return input =>
            {
                if (input is IEnumerable<TFirstIn> inputCollection)
                    foreach (var item in inputCollection)
                        build(item, false);
                else
                    build(input, false);

                build(default, true);

                return this.getEnumerableReturnValues<TOut>(finalStepBuffer, cancel, autoDispose);
            };
        }

        private async IAsyncEnumerable<TOut> getEnumerableReturnValues<TOut>(
            AwaitableBlockingCollection<WorkWrapper<object>> finalBuffer,
            [EnumeratorCancellation] CancellationToken cancel, bool autoDispose)
        {
            while (!finalBuffer.IsCompleted)
            {
                var item = await finalBuffer.TakeAsync(cancel);
                if (finalBuffer.IsCompleted && item == null) yield break;
                switch (item.Work)
                {
                    case null:
                        yield return default;
                        break;
                    case TOut inst:
                        yield return inst;
                        break;
                    case Task<TOut> task:
                        yield return await task;
                        break;
                    case IAsyncEnumerable<TOut> asyncEnum:
                    {
                        await foreach (var outItem in asyncEnum.WithCancellation(cancel)) yield return outItem;

                        break;
                    }
                    case IEnumerable<TOut> syncEnum:
                    {
                        foreach (var outItem in syncEnum) yield return outItem;

                        break;
                    }
                    default:
                        throw new InvalidOperationException(
                            $"Type {item.Work.GetType().FullName} cannot be converted to " +
                            $"{typeof(Task<TOut>).FullName}, {typeof(IAsyncEnumerable<TOut>).FullName}, {typeof(IEnumerable<TOut>).FullName} or {typeof(TOut).FullName}");
                }

                if (!item.CompletionSource.Task.IsCompleted) item.CompletionSource.SetResult(item.Work);
            }

            if (autoDispose) await Task.WhenAll(this.RunAfterCompletion.Select(i => i()));
        }

        private async Task<TOut> getSingleReturnValue<TOut>(
            AwaitableBlockingCollection<WorkWrapper<object>> finalBuffer,
            CancellationToken cancel, bool autoDispose)
        {
            var result = await finalBuffer.TakeAsync(cancel);
            if (!result.CompletionSource.Task.IsCompleted) result.CompletionSource.SetResult(result.Work);

            var output = (TOut) await result.CompletionSource.Task;
            if (autoDispose) await Task.WhenAll(this.RunAfterCompletion.Select(i => i()));

            return output;
        }
    }
}