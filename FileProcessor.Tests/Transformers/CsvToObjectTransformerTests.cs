using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FileProcessor.Providers;
using FileProcessor.Transformers;
using Xunit;

namespace FileProcessor.Tests.Transformers
{
    public class CsvToObjectTransformerTests
    {
        [Fact]
        public async Task ExecuteReadsCsv()
        {
            const string csv = "Foo,Bar,Baz\ntest,1,true\nfoo,2,false";
            var file = Path.GetTempFileName();
            await File.WriteAllTextAsync(file, csv);
            var transformer = new CsvToObjectTransformer<Test>();
            List<Test> results = new List<Test>();
            await foreach (var item in transformer.Execute(new LocalFile(file)))
            {
                results.Add(item);
            }

            Assert.Collection(results, r =>
            {
                Assert.Equal("test", r.Foo);
                Assert.Equal(1, r.Bar);
                Assert.True(r.Baz);
            }, r =>
            {
                Assert.Equal("foo", r.Foo);
                Assert.Equal(2, r.Bar);
                Assert.False(r.Baz);
            });
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        class Test
        {
            public string Foo { get; set; }
            public int Bar { get; set; }
            public bool Baz { get; set; }
        }
    }
}
