using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace FileProcessor.Tests
{
    public class BuilderTests
    {
        private T asyncEnumerableToSingle<T>(IAsyncEnumerable<T> asyncEnumerable)
        {
            var enumerator = asyncEnumerable.GetAsyncEnumerator();
            Assert.True(enumerator.MoveNextAsync().GetAwaiter().GetResult());
            var single = enumerator.Current;
            Assert.False(enumerator.MoveNextAsync().GetAwaiter().GetResult());
            return single;
        }

        [Fact]
        public void AddStepAddsAsyncSteps()
        {
            var builder = new Builder();
            builder.AddStep(new TestAsyncStep());
            Assert.Collection(builder.Steps,
                d => Assert.Equal("cba", this.asyncEnumerableToSingle(d("abc"))));
        }

        [Fact]
        public void AddStepAddsFuncSteps()
        {
            var builder = new Builder();
            builder.AddStep<string, string>(i => new string(i.Reverse().ToArray()));
            Assert.Collection(builder.Steps,
                d => Assert.Equal("cba", this.asyncEnumerableToSingle(d("abc"))));
        }

        [Fact]
        public void AddStepAddsSteps()
        {
            var builder = new Builder();
            builder.AddStep(new TestStep());
            Assert.Collection(builder.Steps,
                d => Assert.Equal("cba", this.asyncEnumerableToSingle(d("abc"))));
        }

        [Fact]
        public void AddStepDoesNotErrorIfStepTypesAreCompatible()
        {
            var builder = new Builder();
            builder.AddStep<string, string>(i => new string(i.Reverse().ToArray()));
            Assert.Null(Record.Exception(() => builder.AddStep<object, int>(i => i.ToString().Length)));
        }

        [Fact]
        public void AddStepErrorsIfStepTypesAreNotCompatible()
        {
            var builder = new Builder();
            builder.AddStep<string, string>(i => new string(i.Reverse().ToArray()));
            Assert.Throws<ArgumentException>(() => builder.AddStep<int, int>(i => i * i));
        }

        [Fact]
        public async Task BuildCallsStepsReturnsData()
        {
            const string abc = "abc";
            const string cba = "cba";
            var builder = new Builder();
            var mockReverseStep = new Mock<Func<string, string>>();
            mockReverseStep.Setup(i => i(abc)).Returns(cba);
            var mockCountStep = new Mock<Func<string, Tuple<int, string>>>();
            mockCountStep.Setup(i => i(cba)).Returns(new Tuple<int, string>(3, cba));

            var fluentProcessor = builder.AddStep(mockReverseStep.Object)
                .AddStep(mockCountStep.Object)
                .Returns<Tuple<int, string>>();
            var processor = builder.Build<string, Tuple<int, string>>(CancellationToken.None, true);

            var result = await processor(abc);
            Assert.Equal(new Tuple<int, string>(3, cba), result);
            var fluentResult = await fluentProcessor(abc);
            Assert.Equal(new Tuple<int, string>(3, cba), fluentResult);
            mockReverseStep.Verify(s => s(abc), Times.Exactly(2));
            mockCountStep.Verify(s => s(cba), Times.Exactly(2));
        }

        [Fact]
        public async Task ReturnsEnumerableReturnsIndividualResults()
        {
            const string word = "hello";
            var rnd = new Random();
            var delays = word.Select(_ => rnd.Next(300, 3000)).ToArray();
            var delaysQueue = new Queue<int>(delays);
            var processor = new Builder()
                .AcceptCollection<char>()
                .AddStep(char.ToUpper)
                .AddStep(async i =>
                {
                    var msDelay = delaysQueue.Dequeue();
                    await Task.Delay(msDelay);
                    return i;
                }).ReturnsAsyncEnumerable<char>();

            var result = processor(word);

            var swAll = Stopwatch.StartNew();
            var swFirst = Stopwatch.StartNew();
            var resultText = string.Empty;
            await foreach (var c in result)
            {
                if (resultText.Length == 0) swFirst.Stop();

                resultText += c;
            }

            swAll.Stop();

            Assert.Equal(word.ToUpper(), resultText);
            Assert.True(swAll.ElapsedMilliseconds > swFirst.ElapsedMilliseconds);
            Assert.True(swAll.ElapsedMilliseconds >= delays.Sum());
        }


        [Fact]
        public async Task ReturnsEnumerableReturnsMultipleResults()
        {
            const string word = "hello";
            var rnd = new Random();
            var delays = (word + word).Select(_ => rnd.Next(300, 3000)).ToArray();
            var delaysQueue = new Queue<int>(delays);
            var step = new TestAsyncEnumerableStep(delaysQueue);
            int firstCounter = 0, lastCounter = 0;
            var processor = new Builder()
                .AcceptCollection<char>()
                .AddStep(i =>
                {
                    firstCounter++;
                    return i;
                })
                .AddStep(step)
                .AddStep(i =>
                {
                    lastCounter++;
                    return i;
                })
                .ReturnsAsyncEnumerable<char>();

            var result = processor(word);

            var swAll = Stopwatch.StartNew();
            var swFirst = Stopwatch.StartNew();
            var resultText = string.Empty;

            var counter = 0;
            await foreach (var c in result)
            {
                if (counter == 0) swFirst.Stop();

                counter++;
                if (counter % 2 == 0)
                    Assert.Equal(counter, step.ReturnedValues);
                else
                    // step produces values two at a time but we process them one at a time
                    Assert.Equal(counter + 1, step.ReturnedValues);
                Assert.Equal(lastCounter, step.ReturnedValues);

                resultText += c;
            }

            swAll.Stop();

            Assert.Equal("hHeElLlLoO", resultText);
            Assert.Equal(word.Length, firstCounter);
            Assert.Equal(word.Length * 2, lastCounter);
            Assert.True(swAll.ElapsedMilliseconds > swFirst.ElapsedMilliseconds);
            Assert.True(swAll.ElapsedMilliseconds > delays.Max());
            Assert.True(swAll.ElapsedMilliseconds < delays.Sum());
        }

        [Fact]
        public async Task ReturnsEnumeratesCorrectly()
        {
            var counter = 0;
            var result = await new Builder().AddStep<int, IEnumerable<int>>(i =>
                    Enumerable.Range(1, i).Select(i => counter = i))
                .Returns<IEnumerable<int>>()(10);

            Assert.Equal(0, counter);
            Assert.Equal(1, result.First());
            Assert.Equal(1, counter);
        }
    }

    internal class TestStep : IStep<string, string>, IDisposable
    {
        public bool Disposed { get; set; }

        public void Dispose()
        {
            this.Disposed = true;
        }

        public string Execute(string input)
        {
            if (this.Disposed) throw new ObjectDisposedException(null);
            return new string(input.Reverse().ToArray());
        }
    }

    internal class TestAsyncStep : IAsyncStep<string, string>, IAsyncDisposable
    {
        public bool Disposed { get; set; }

        public ValueTask DisposeAsync()
        {
            this.Disposed = true;
            return new ValueTask();
        }

        public async Task<string> Execute(string input)
        {
            if (this.Disposed) throw new ObjectDisposedException(null);
            return await Task.FromResult(new string(input.Reverse().ToArray()));
        }
    }

    internal class TestAsyncEnumerableStep : IAsyncEnumerableStep<char, char>, IDisposable
    {
        private readonly Queue<int> delaysQueue;

        public TestAsyncEnumerableStep(Queue<int> delaysQueue)
        {
            this.delaysQueue = delaysQueue;
        }

        public int ReturnedValues { get; set; }

        public bool Disposed { get; set; }

        public async IAsyncEnumerable<char> Execute(char input)
        {
            await Task.Delay(this.delaysQueue.Dequeue());
            this.ReturnedValues++;
            if (this.Disposed) throw new ObjectDisposedException(null);
            yield return char.ToLower(input);
            this.ReturnedValues++;
            if (this.Disposed) throw new ObjectDisposedException(null);
            yield return char.ToUpper(input);
        }

        public void Dispose()
        {
            this.Disposed = true;
        }
    }
}