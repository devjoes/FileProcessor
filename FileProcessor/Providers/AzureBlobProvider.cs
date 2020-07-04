using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FileProcessor.Providers
{
    public class AzureBlobProvider : IAsyncEnumerableStep<AzureBlobProviderOptions, IFileReference>, IAsyncDisposable
    {
        private readonly BlobContainerClient client;
        private readonly ILogger logger;
        private readonly bool downloadFilesOnceFound;
        private readonly CompositeDisposable toDispose;

        public AzureBlobProvider(Uri containerUri, ILogger logger, bool downloadFilesOnceFound = true)
        {
            this.logger = logger;
            this.downloadFilesOnceFound = downloadFilesOnceFound;
            this.client = new BlobContainerClient(containerUri, new DefaultAzureCredential());
            this.toDispose = new CompositeDisposable();
        }

        public AzureBlobProvider(string connectionString, string blobContainerName, ILogger logger, bool downloadFilesOnceFound = true)
        {
            this.logger = logger;
            this.downloadFilesOnceFound = downloadFilesOnceFound;
            this.client = new BlobContainerClient(connectionString, blobContainerName);
            this.toDispose = new CompositeDisposable();
        }

        public virtual async IAsyncEnumerable<IFileReference> Execute(AzureBlobProviderOptions input)
        {
            this.logger?.LogTrace("Execute");
            AsyncPageable<BlobItem> blobs;
            Regex rx;
            try
            {
                blobs = this.client.GetBlobsAsync(prefix: input.Prefix);
                rx = new Regex(string.IsNullOrEmpty(input.NameRegex) ? "." : input.NameRegex);
            }
            catch (Exception ex)
            {
                this.logger?.LogError(ex.Message + "\n" + ex.StackTrace);
                throw;
            }
            await foreach (var blob in blobs)
            {
                if (rx.IsMatch(blob.Name))
                {
                    yield return await AzureBlob.FromBlob(blob, this.client, this.downloadFilesOnceFound);
                }

            }


        }

        public virtual async ValueTask DisposeAsync()
        {
            await this.toDispose.DisposeAsync();
        }
    }

    public class AzureBlob : IFileReference, IDisposable
    {
        private readonly BlobItem blobItem;
        private string tmp;
        private readonly BlobContainerClient client;

        private AzureBlob(BlobItem blobItem, BlobContainerClient client)
        {
            this.blobItem = blobItem;
            this.client = client;
        }

        public void Dispose()
        {
            if (this.tmp == null)
            {
                return;
            }

            string toDel = this.tmp;
            try
            {
                File.Delete(toDel);
                this.tmp = null;
            }
            catch
            {
                // ignored
            }
        }

        public virtual async Task<FileInfo> GetLocalFileInfo()
        {
            if (this.tmp == null) await this.Download();

            return new FileInfo(this.tmp);
        }

        public string FileReference => this.blobItem.Name;

        public static async Task<AzureBlob> FromBlob(BlobItem blobItem, BlobContainerClient client, bool download)
        {
            var blob = new AzureBlob(blobItem, client);
            if (download) await blob.Download();
            return blob;
        }

        protected virtual async Task Download()
        {
            this.tmp = Path.GetTempFileName();
            await this.client.GetBlobClient(this.blobItem.Name).DownloadToAsync(this.tmp);
        }
    }

    public class AzureBlobProviderOptions
    {
        public Dictionary<string, string[]> SharesToPaths { get; set; }
        public string Prefix { get; set; }
        public string NameRegex { get; set; }
    }
}