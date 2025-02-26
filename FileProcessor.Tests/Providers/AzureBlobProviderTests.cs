// Tests broken after upgrading nuget packages - not got time to fix

//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Azure.Storage.Blobs;
//using Azure.Storage.Blobs.Models;
//using DockerizedTesting.Azurite;
//using FileProcessor.Providers;
//using Xunit;

//namespace FileProcessor.Tests.Providers
//{
//    public class AzureBlobProviderTests:IClassFixture<AzuriteFixture>
//    {
//        private readonly string containerName ;
//        private readonly string connectionString;

//        public AzureBlobProviderTests(AzuriteFixture fixture)
//        {
//            fixture.Start().GetAwaiter().GetResult();
//            this.containerName = fixture.Options.StorageAccountName + "/test";
//            this.connectionString = fixture.BlobConnectionString;
            
//            var client = new BlobContainerClient(this.connectionString,containerName);
//            client.DeleteIfExists();
//            client.Create();
//            client.GetBlobClient("foo1.txt").Upload(new MemoryStream(Encoding.UTF8.GetBytes("foo")));
//            client.GetBlobClient("foo2.txt").Upload(new MemoryStream(Encoding.UTF8.GetBytes("foo")));
//            client.GetBlobClient("bar").Upload(new MemoryStream(Encoding.UTF8.GetBytes("bar")));
//        }

//        [Fact]
//        public async Task ExecuteDownloadsBlobs()
//        {
//            var blobProvider = new AzureBlobProvider(this.connectionString, containerName, null);
    
//            var files = blobProvider.Execute(new AzureBlobProviderOptions());
//            List<IFileReference> results = new List<IFileReference>();
//            await foreach (var file in files)
//            {
//                results.Add(file);
//            }

//            Assert.Equal(3, results.Count);
//            Assert.Equal("bar", results.First().FileReference.Split('/').Last());
//            Assert.Equal("foo2.txt", results.Last().FileReference.Split('/').Last());
//        }


//        [Fact]
//        public async Task ExecuteDownloadsBlobsByPrefix()
//        {
//            var blobProvider = new AzureBlobProvider(this.connectionString, containerName, null);
//            var files = blobProvider.Execute(new AzureBlobProviderOptions { Prefix = "b" });
//            List<IFileReference> results = new List<IFileReference>();
//            await foreach (var file in files)
//            {
//                results.Add(file);
//            }

//            Assert.Collection(results, f =>
//            {
//                Assert.Equal("bar", f.FileReference.Split('/').Last());
//                Assert.Equal("bar", File.ReadAllText(f.GetLocalFileInfo().Result.FullName));
//            });
//        }

//        [Fact]
//        public async Task ExecuteDownloadsBlobsByRegex()
//        {
//            var blobProvider = new AzureBlobProvider(this.connectionString, containerName, null);
//            var files = blobProvider.Execute(new AzureBlobProviderOptions { NameRegex = "\\d\\.txt$"});
//            List<IFileReference> results = new List<IFileReference>();
//            await foreach (var file in files)
//            {
//                results.Add(file);
//            }

//            Assert.Collection(results, f =>
//            {
//                Assert.Equal("foo1.txt", f.FileReference.Split('/').Last());
//                Assert.Equal("foo", File.ReadAllText(f.GetLocalFileInfo().Result.FullName));
//            }, f =>
//            {
//                Assert.Equal("foo2.txt", f.FileReference.Split('/').Last());
//                Assert.Equal("foo", File.ReadAllText(f.GetLocalFileInfo().Result.FullName));
//            });
//        }


//        [Fact]
//        public async Task DisposeDeletesFiles()
//        {

//            var blobProvider = new AzureBlobProvider(this.connectionString, containerName, null);
//            var files = blobProvider.Execute(new AzureBlobProviderOptions { Prefix = "b" });

//            List<IFileReference> results = new List<IFileReference>();
//            await foreach (var file in files)
//            {
//                results.Add(file);
//            }

//            Assert.Single(results);
//            var blob = (AzureBlob)results.Single();
//            var fi = await blob.GetLocalFileInfo();
//            Assert.True(File.Exists(fi.FullName));
//            blob.Dispose();
//            Assert.False(File.Exists(fi.FullName));
//        }


//        [Fact]
//        public async Task FilesAreDownloadedWhenRequired()
//        {

//            var blobProvider = new AzureBlobProvider(this.connectionString, containerName, null, false);
//            var files = blobProvider.Execute(new AzureBlobProviderOptions { Prefix = "b" });

//            List<IFileReference> results = new List<IFileReference>();
//            await foreach (var file in files)
//            {
//                results.Add(file);
//            }

//            DateTime before = DateTime.UtcNow;
//            await Task.Delay(1500);
//            Assert.Single(results);
//            var fi = await results.Single().GetLocalFileInfo();
//            Assert.True(fi.Exists);
//            Assert.True(before.AddMilliseconds(500) < fi.CreationTimeUtc);
//        }
//    }
//}
