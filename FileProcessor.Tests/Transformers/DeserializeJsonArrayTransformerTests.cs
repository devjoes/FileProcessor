using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileProcessor.Providers;
using FileProcessor.Transformers;
using Newtonsoft.Json;
using Xunit;

namespace FileProcessor.Tests.Transformers
{
    public class DeserializeJsonArrayTransformerTests
    {
        private async Task<string> generateData(int count, string before = "[", string after = "\n]",
            JsonSerializerSettings settings = null)
        {
            var file = Path.GetTempFileName();
            await using var writer = new StreamWriter(file);
            await writer.WriteLineAsync(before);
            for (var i = 1; i <= count; i++)
            {
                var data = new Test
                {
                    Id = i,
                    Text = Guid.NewGuid().ToString(),
                    When = DateTime.UtcNow,
                    Children = new[] {new Test {Text = Guid.NewGuid().ToString()}}
                };
                await writer.WriteAsync(JsonConvert.SerializeObject(data, settings ?? new JsonSerializerSettings()));
                if (i < count) await writer.WriteLineAsync(",");
            }

            await writer.WriteLineAsync(after);
            writer.Close();
            return file;
        }

        [Fact]
        public async Task ExecuteDeserializesNestedArray()
        {
            const int count = 10;
            var transformer = new DeserializeJsonArrayTransformer<Test>(new DeserializeJsonArrayTransformerOptions
                {JsonPath = "bar[0].baz"});
            var path = await this.generateData(count, "{\"foo\":1, \"bar\": [ {\"baz\":[",
                "]} , 1,2,3], \"blah\": {\"blah\" : \"blah\"}}");

            var output = transformer.Execute(new LocalFile(path));
            var results = new List<Test>();

            await foreach (var item in output) results.Add(item);

            Assert.Equal(count, results.Count);
            Assert.All(results, i =>
            {
                Assert.NotEqual(0, i.Id);
                Assert.NotNull(i.Text);
                Assert.NotNull(i.Children.First().Text);
                Assert.NotEqual(default, i.When);
            });
        }

        [Fact]
        public async Task ExecuteDeserializesSimpleArray()
        {
            const int count = 10;
            var transformer = new DeserializeJsonArrayTransformer<Test>(new DeserializeJsonArrayTransformerOptions());
            var path = await this.generateData(count);

            var output = transformer.Execute(new LocalFile(path));
            var results = new List<Test>();
            await foreach (var item in output) results.Add(item);

            Assert.Equal(count, results.Count);
            Assert.All(results, i =>
            {
                Assert.NotEqual(0, i.Id);
                Assert.NotNull(i.Text);
                Assert.NotNull(i.Children.First().Text);
                Assert.NotEqual(default, i.When);
            });
        }

        [Fact]
        public async Task ExecuteDeserializesWithCustomSettings()
        {
            const int count = 10;
            var settings = new JsonSerializerSettings {DateFormatString = "dd/yyyy/MM"};
            var transformer = new DeserializeJsonArrayTransformer<Test>(new DeserializeJsonArrayTransformerOptions
                {SerializerSettings = settings});
            var path = await this.generateData(count, "[", "]", settings);

            var output = transformer.Execute(new LocalFile(path));
            var results = new List<Test>();
            await foreach (var item in output) results.Add(item);

            Assert.Equal(count, results.Count);
            Assert.All(results, i =>
            {
                Assert.NotEqual(0, i.Id);
                Assert.NotNull(i.Text);
                Assert.NotNull(i.Children.First().Text);
                Assert.NotEqual(default, i.When);
            });
        }
    }

    public class Test
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public DateTime When { get; set; }
        public Test[] Children { get; set; }
    }
}