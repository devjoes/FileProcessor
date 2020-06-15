using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FileProcessor.Providers;
using Xunit;

namespace FileProcessor.Tests.Providers
{
    public class AzureFileProviderTests
    {

        [Fact]
        public async Task ExecuteReturnsSingleFile()
        {
            const string share = "share";
            const string path = "/foo.txt";
            const string foo = "foo";
            var client = MockCloudFileClient.MockClient(share, String.Empty, new Dictionary<string, string>
            {
                {$"/{share}/{path.Trim('/')}", foo}
            });

            var processor = new AzureFileProvider(client);
            var result = processor.Execute(new AzureFileProviderOptions
            {
                SharesToPaths = new Dictionary<string, string[]>
                {
                    {share, new[] {path}}
                }
            });
            Dictionary<string, string> files = new Dictionary<string, string>();
            await foreach (var i in result)
            {
                var fileInfo = await i.GetLocalFileInfo();
                using var reader = fileInfo.OpenText();
                files.Add(i.FileReference, await reader.ReadToEndAsync());
            }
            Assert.Single(files);
            Assert.Collection(files, pair => Assert.Equal($"{MockCloudFileClient.Domain}/{share}{path}", pair.Key));
            Assert.Collection(files, pair => Assert.Equal(foo, pair.Value));
        }

        [Fact]
        public async Task ExecuteReturnsMultipleFiles()
        {
            const string share = "share";
            const string fooPath = "/foo.txt";
            const string barPath = "/bar.txt";
            const string foo = "foo";
            const string bar = "bar";
            var client = MockCloudFileClient.MockClient(share, String.Empty, new Dictionary<string, string>
            {
                {$"/{share}/{fooPath.Trim('/')}", foo},
                {$"/{share}/{barPath.Trim('/')}", bar},
                {$"/{share}/baz.jpg", "baz"}
            });

            var processor = new AzureFileProvider(client);
            var result = processor.Execute(new AzureFileProviderOptions
            {
                SharesToPaths = new Dictionary<string, string[]>
                {
                    {share, new[] {"/*.txt"}},
                }
            });
            Dictionary<string, string> files = new Dictionary<string, string>();
            await foreach (var i in result)
            {
                var fileInfo = await i.GetLocalFileInfo();
                using var reader = fileInfo.OpenText();
                files.Add(i.FileReference, await reader.ReadToEndAsync());
            }

            Assert.Equal(2, files.Count);
            Assert.Equal(foo, files[$"{MockCloudFileClient.Domain}/{share}{fooPath}"]);
            Assert.Equal(bar, files[$"{MockCloudFileClient.Domain}/{share}{barPath}"]);
        }

        [Fact]
        public async Task ExecuteReturnsMultipleFilesFromSubDir()
        {
            const string share = "share";
            const string fooPath = "/sub/foo.txt";
            const string barPath = "/sub/bar.txt";
            const string foo = "foo";
            const string bar = "bar";
            var client = MockCloudFileClient.MockClient(share, String.Empty, new Dictionary<string, string>
            {
                {$"/{share}/{fooPath.Trim('/')}", foo},
                {$"/{share}/{barPath.Trim('/')}", bar},
                {$"/{share}/sub/baz.jpg", "baz"},
                {$"/{share}/baz.jpg", "baz"}
            });

            var processor = new AzureFileProvider(client);
            var result = processor.Execute(new AzureFileProviderOptions
            {
                SharesToPaths = new Dictionary<string, string[]>
                {
                    {share, new[] {"/sub/*.txt"}},
                }
            });
            Dictionary<string, string> files = new Dictionary<string, string>();
            await foreach (var i in result)
            {
                var fileInfo = await i.GetLocalFileInfo();
                using var reader = fileInfo.OpenText();
                files.Add(i.FileReference, await reader.ReadToEndAsync());
            }

            Assert.Equal(2, files.Count);
            Assert.Equal(foo, files[$"{MockCloudFileClient.Domain}/{share}{fooPath}"]);
            Assert.Equal(bar, files[$"{MockCloudFileClient.Domain}/{share}{barPath}"]);
        }


        [Fact]
        public async Task ExecuteReturnsMultipleFilesFromDifferentSubDirsWithWildcards()
        {
            const string share = "share";
            const string fooPath = "/sub/foo.txt";
            const string barPath = "/bar.txt";
            const string bazPath = "/sub/very/deep/dir/baz.pdf";
            const string foo = "foo";
            const string bar = "bar";
            const string baz = "baz";
            var client = MockCloudFileClient.MockClient(share, String.Empty, new Dictionary<string, string>
            {
                {$"/{share}/{fooPath.Trim('/')}", foo},
                {$"/{share}/{barPath.Trim('/')}", bar},
                {$"/{share}/{bazPath.Trim('/')}", baz},
                {$"/{share}/sub/baz.jpg", "baz"},
                {$"/{share}/sub2/baz.jpg", "baz"},
                {$"/{share}/sub/blah/baz.txt", "baz"},
                {$"/{share}/baz.jpg", "baz"}
            });

            var processor = new AzureFileProvider(client);
            var result = processor.Execute(new AzureFileProviderOptions
            {
                SharesToPaths = new Dictionary<string, string[]>
                {
                    {share, new[] {"/*.txt", "/*/*.txt","/*/very/deep/dir/baz.pdf"}},
                }
            });
            Dictionary<string, string> files = new Dictionary<string, string>();
            await foreach (var i in result)
            {
                var fileInfo = await i.GetLocalFileInfo();
                using var reader = fileInfo.OpenText();
                files.Add(i.FileReference, await reader.ReadToEndAsync());
            }

            Assert.Equal(3, files.Count);
            Assert.Equal(foo, files[$"{MockCloudFileClient.Domain}/{share}{fooPath}"]);
            Assert.Equal(bar, files[$"{MockCloudFileClient.Domain}/{share}{barPath}"]);
            Assert.Equal(baz, files[$"{MockCloudFileClient.Domain}/{share}{bazPath}"]);
        }
    }
}
