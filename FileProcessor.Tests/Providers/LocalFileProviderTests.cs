using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileProcessor.Providers;
using Xunit;

namespace FileProcessor.Tests.Providers
{
    public class LocalFileProviderTests
    {
        [Fact]
        public async Task ExecuteReadsFilesInDirectory()
        {
            const string foo = "foo";
            var provider = new LocalFileProvider();
            var tempFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tempFile, foo);
                var dirPath = Path.GetDirectoryName(tempFile);
                var result = provider.Execute(new LocalFileProviderOptions {Path = dirPath});
                var foundFile = result.SingleOrDefault(l => l.FileReference == tempFile);

                Assert.NotNull(foundFile);
                var fileInfo = await foundFile.GetLocalFileInfo();
                using var reader = fileInfo.OpenText();
                Assert.Equal(foo, await reader.ReadToEndAsync());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}