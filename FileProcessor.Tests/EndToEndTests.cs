using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FileProcessor.Tests
{
    public class EndToEndTests
    {
        public EndToEndTests()
        {
        }
        [Fact]
        public async Task HandlesErrors()
        {
            var process = new Builder().AddStep<int, IEnumerable<int>>(i =>
            {
                if (i == 1)
                {
                    throw new AccessViolationException("foo");
                }

                return new[] { i };
            }).Returns<IEnumerable<int>>();

            var ex = await Assert.ThrowsAsync<AccessViolationException>(async () => await process(1));
            Assert.Equal("foo", ex.Message);
        }

        [Fact]
        public async Task ComplexTest()
        {
            var process = new Builder().AddStep<int, IEnumerable<int>>(i
                    => Enumerable.Range(2, (int)Math.Floor(i / 2d)).Where(f => i % f == 0))
                .AddStep<string>(i => string.Join(',', i.Select(n => n.ToString())))
                .AddStep(new TestAsyncStep())
                .AddStep(i => i.Length == 1 ? new[] { i } : new[] { i.Remove(i.Length / 2), i.Substring(i.Length / 2) })
                .Returns<string[]>();

            var result = await process(48);
            Assert.Equal("8,6,4,3,2", result.Last());
        }


        [Fact]
        public async Task AutoDisposeTest()
        {
            var step = new TestStep();
            var asyncStep = new TestAsyncStep();
            var process = new Builder()
                .AddStep(step)
                .AddStep(asyncStep)
                .Returns<string>();

            Assert.False(step.Disposed);
            Assert.False(asyncStep.Disposed);
            var result = await process("abc");
            Assert.True(step.Disposed);
            Assert.True(asyncStep.Disposed);

            Assert.Equal("abc", result);
        }

        [Fact]
        public async Task ManualDisposeTest()
        {
            var step = new TestStep();
            var asyncStep = new TestAsyncStep();
            var builder = new Builder();
            var process = builder
                .AddStep(step)
                .AddStep(asyncStep)
                .Returns<string>(CancellationToken.None, false);

            Assert.False(step.Disposed);
            Assert.False(asyncStep.Disposed);
            var result = await process("abc");
            Assert.False(step.Disposed);
            Assert.False(asyncStep.Disposed);
            builder.Dispose();
            Assert.True(step.Disposed);
            Assert.True(asyncStep.Disposed);
            Assert.Equal("abc", result);

            builder = new Builder();
            process = builder
                .AddStep(step)
                .AddStep(asyncStep)
                .Returns<string>(CancellationToken.None, false);
            builder.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await process("bang"));
        }

        [Fact]
        public async Task ConsumesEnumerable()
        {
            var process = new Builder()
                .AcceptCollection<int>()
                .AddStep<string>(i => i.ToString())
                .ReturnsAsyncEnumerable<string>();

            var output = process(Enumerable.Range(0, 10));
            int counter = 0;
            await foreach (var item in output)
            {
                Assert.Equal(counter.ToString(), item);
                counter++;
            }
            Assert.Equal(10, counter);
        }
        
        // exact timings vary depending on core count/load etc so the timings here are pretty vague
        [Theory]
        [InlineData(false, 1, 1, 10, 11)]
        [InlineData(true, 1, 1, 10, 11)]
        [InlineData(false, 5, 1, 1, 3)]
        [InlineData(true, 5, 1, 1, 3)]
        [InlineData(false, 5, 2, 2, 4)]
        [InlineData(true, 5, 2, 2, 4)]
        [InlineData(true, 5, 3, 5, 6)]
        public async Task ParallelExecutionTest(bool sync, int parallelism, int stepCount, int minSecs, int maxSecs)
        {
            var asyncStep = new Func<int, Task<int>>(async i =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                return i;
            });
            var syncStep = new Func<int, int>(i =>
            {
                Task.Delay(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
                return i;
            });

            Stopwatch timer = Stopwatch.StartNew();

            var stepOptions = new StepOptions { Parallelism = parallelism };
            var builder = new Builder().AcceptCollection<int>();
            for (int i = 0; i < stepCount; i++)
            {
                builder = sync
                    ? builder.AddStep(syncStep, stepOptions)
                    : builder.AddStep(asyncStep, stepOptions);
            }

            var process = builder.ReturnsAsyncEnumerable<int>();
            var input = Enumerable.Range(1, 10).ToArray();
            var enumerator = process(input);

            int sum = 0;
            await foreach (var i in enumerator)
            {
                sum += i;
            }
            timer.Stop();
            Assert.True(minSecs <= timer.Elapsed.Seconds, timer.Elapsed.ToString());
            Assert.True(maxSecs >= timer.Elapsed.Seconds, timer.Elapsed.ToString());
            Assert.Equal(input.Sum(), sum);
        }

    }
}
