using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace FileProcessor.Tests
{
    public class BuilderTests
    {
        [Fact]
        public void AddStepAddsFuncSteps()
        {
            var builder = new Builder();
            builder.AddStep<string, string>(i => new String(i.Reverse().ToArray()));
            Assert.Collection(builder.Steps,
                d => Assert.Equal("cba", asyncEnumerableToSingle(d("abc"))));
        }

        [Fact]
        public void AddStepAddsSteps()
        {
            var builder = new Builder();
            builder.AddStep(new TestStep());
            Assert.Collection(builder.Steps,
                d => Assert.Equal("cba", asyncEnumerableToSingle(d("abc"))));
        }

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
                d => Assert.Equal("cba", asyncEnumerableToSingle(d("abc"))));
        }

        [Fact]
        public void AddStepErrorsIfStepTypesAreNotCompatible()
        {
            var builder = new Builder();
            builder.AddStep<string, string>(i => new String(i.Reverse().ToArray()));
            Assert.Throws<ArgumentException>(() => builder.AddStep<int, int>(i => i * i));
        }

        [Fact]
        public void AddStepDoesNotErrorIfStepTypesAreCompatible()
        {
            var builder = new Builder();
            builder.AddStep<string, string>(i => new String(i.Reverse().ToArray()));
            Assert.Null(Record.Exception(() => builder.AddStep<object, int>(i => i.ToString().Length)));
        }

        [Fact]
        public async Task BuildCallsStepsReturnsData()
        {
            const string abc = "abc";
            const string cba = "cba";
            var builder = new Builder();
            var mockReverseStep = new Mock<Func<string, string>>();
            mockReverseStep.Setup(i => i(abc)).Returns(cba);
            var mockCountStep = new Mock<Func<String, Tuple<int, string>>>();
            mockCountStep.Setup(i => i(cba)).Returns(new Tuple<int, string>(3, cba));

            var fluentProcessor = builder.AddStep<string, string>(mockReverseStep.Object)
                .AddStep(mockCountStep.Object)
                .Returns<Tuple<int, string>>();
            var processor = builder.Build<String, Tuple<int, string>>();

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
            var processor = new Builder().AddStep<char, char>(Char.ToUpper)
                .AddStep(async i =>
                {
                    var msDelay = delaysQueue.Dequeue();
                    await Task.Delay(msDelay);
                    return i;
                }).ReturnsAsyncEnumerable<char>();

            var result = processor(word);

            var swAll = Stopwatch.StartNew();
            var swFirst = Stopwatch.StartNew();
            string resultText = string.Empty;
            await foreach (var c in result)
            {
                if (resultText.Length == 0)
                {
                    swFirst.Stop();
                }

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
            var delays = (word+word).Select(_ => rnd.Next(300, 3000)).ToArray();
            var delaysQueue = new Queue<int>(delays);
            var step = new TestAsyncEnumerableStep(delaysQueue);
            var processor = new Builder()
                .AddStep(step)
                .ReturnsAsyncEnumerable<char>();

            var result = processor(word);

            var swAll = Stopwatch.StartNew();
            var swFirst = Stopwatch.StartNew();
            string resultText = string.Empty;

            int counter = 0;
            await foreach (var c in result)
            {
                if (counter == 0)
                {
                    swFirst.Stop();
                }
                Assert.Equal(++counter, step.ReturnedValues);

                resultText += c;
            }
            swAll.Stop();

            Assert.Equal("hHeElLlLoO", resultText);
            Assert.True(swAll.ElapsedMilliseconds > swFirst.ElapsedMilliseconds);
            Assert.True(swAll.ElapsedMilliseconds > delays.Max());
            Assert.True(swAll.ElapsedMilliseconds < delays.Sum());
        }

        [Fact]
        public async Task ReturnsEnumeratesCorrectly()
        {
            int counter = 0;
            var result = await new Builder().AddStep<int, IEnumerable<int>>(i =>
                    Enumerable.Range(1, i).Select(i => counter = i))
                .Returns<IEnumerable<int>>()(10);

            Assert.Equal(0,counter);
            Assert.Equal(1, result.First());
            Assert.Equal(1, counter);
        }

        [Fact]
        public async Task HandlesErrors()
        {
            //TODO: Dont forget dns zone
            var process = new Builder().AddStep<int, IEnumerable<int>>(i =>
            {
                if (i == 1)
                {
                    throw new AccessViolationException("foo");
                }

                return new []{i};
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
    }

    class TestStep : IStep<string, string>
    {
        public string Execute(string input)
        {
            return new string(input.Reverse().ToArray());
        }
    }
    class TestAsyncStep : IAsyncStep<string, string>
    {
        public async Task<string> Execute(string input)
        {
            return await Task.FromResult(new string(input.Reverse().ToArray()));
        }
    }

    class TestAsyncEnumerableStep : IAsyncEnumerableStep<char, char>
    {
        private readonly Queue<int> delaysQueue;

        public TestAsyncEnumerableStep(Queue<int> delaysQueue)
        {
            this.delaysQueue = delaysQueue;
        }
        public async IAsyncEnumerable<char> Execute(char input)
        {
            await Task.Delay(this.delaysQueue.Dequeue());
            this.ReturnedValues++;
            yield return char.ToLower(input);
            this.ReturnedValues++;
            yield return char.ToUpper(input);
        }

        public int ReturnedValues { get; set; }

    }
}
