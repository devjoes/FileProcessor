using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using FileProcessor.Providers;
using FileProcessor.Transformers;
using Xunit;

namespace FileProcessor.Tests.Transformers
{
    public class UnZipTransformerTests
    {
        [Fact]
        public async Task ExecuteUnzipsFile()
        {
            const string foo = "foo";
            const string fooTxt = "foo.txt";
            const string bar = "bar";
            const string barTxt = "bar.txt";

            var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var zip = $"{tmpDir}.zip";
            Directory.CreateDirectory(tmpDir);
            try
            {
                await File.WriteAllTextAsync(Path.Combine(tmpDir, fooTxt), foo);
                await File.WriteAllTextAsync(Path.Combine(tmpDir, barTxt), bar);
                ZipFile.CreateFromDirectory(tmpDir, zip);

                var transformer = new UnZipTransformer();
                var results = new Dictionary<string, string>();
                await foreach (var file in transformer.Execute(new LocalFile(zip)))
                    results.Add(Path.GetFileName(file.FileReference)!, await File.ReadAllTextAsync(file.FileReference));

                Assert.Equal(2, results.Count);
                Assert.Equal(foo, results[fooTxt]);
                Assert.Equal(bar, results[barTxt]);
            }
            finally
            {
                Directory.Delete(tmpDir, true);
            }
        }
    }
}