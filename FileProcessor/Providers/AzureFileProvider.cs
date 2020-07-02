﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.File;
using Newtonsoft.Json;

namespace FileProcessor.Providers
{
    public class AzureFileProvider : IAsyncEnumerableStep<AzureFileProviderOptions, IFileReference>, IAsyncDisposable
    {
        private readonly CloudFileClient client;
        private readonly bool downloadFilesOnceFound;
        private readonly CompositeDisposable toDispose;

        public AzureFileProvider(string connectionString, bool authenticateWithMsi = false,
            bool downloadFilesOnceFound = true)
        {
            this.downloadFilesOnceFound = downloadFilesOnceFound;
            var storageAccount = authenticateWithMsi
                ? new CloudStorageAccount(
                    new StorageCredentials(new TokenCredential(getMsiToken("https://blob.core.windows.net/"))), true)
                : CloudStorageAccount.Parse(connectionString);

            this.toDispose = new CompositeDisposable();
            this.client = storageAccount.CreateCloudFileClient();
        }

        public AzureFileProvider(CloudFileClient client)
        {
            this.toDispose = new CompositeDisposable();
            this.client = client;
        }

        public virtual async IAsyncEnumerable<IFileReference> Execute(AzureFileProviderOptions input)
        {
            foreach (var shareName in input.SharesToPaths.Keys)
            {
                var share = this.client.GetShareReference(shareName);
                var root = share.GetRootDirectoryReference();
                await foreach (var file in this.ProcessDir(root,
                    input.SharesToPaths[shareName].OrderBy(p => p).ToArray()))
                {
                    yield return file;
                }
            }
        }

        protected virtual async IAsyncEnumerable<IFileReference> ProcessDir(CloudFileDirectory dir, string[] pathPatterns)
        {
            //TODO: Poss optimize this
            var dirContents = dir.ListFilesAndDirectories().ToArray();

            var files = dirContents.OfType<CloudFile>();
            await foreach (var file in this.GetFiles(dir, pathPatterns, files.ToArray())) yield return file;

            //if (pathPatterns.Select(p => p.Trim('/')).Any(p => p.IndexOf('/') != p.LastIndexOf('/')))
            //{
            await foreach (var file in this.ProcessSubDirs(pathPatterns, dirContents)) yield return file;
            //}
        }

       protected virtual async IAsyncEnumerable<IFileReference> ProcessSubDirs(string[] pathPatterns,
            IListFileItem[] dirContents)
        {
            var dirPatterns = pathPatterns
                .Select(p => p.Trim('/'))
                .Where(p => p.Contains('/'))
                .ToArray();
            if (!dirPatterns.Any()) yield break;

            var rxsToPatterns = dirPatterns.GroupBy(p => p.Split('/').First())
                .Select(g => (new Regex(Regex.Escape(g.Key).Replace("\\*", "[^\\/]*")), g.ToArray())).ToArray();

            var subDirs = dirContents.OfType<CloudFileDirectory>();
            foreach (var (subDir, matchingPatterns) in subDirs
                .Select(d => (d, rxsToPatterns.Where(rx => rx.Item1.IsMatch(d.Name))
                    .SelectMany(i => i.Item2)))
                .Where(i => i.Item2.Any()))
            {
                var nextPatterns = matchingPatterns.Select(p =>
                    string.Join('/', p.Split('/').Skip(1)));
                await foreach (var file in this.ProcessDir(subDir, nextPatterns.ToArray())) yield return file;
            }
        }

       protected virtual async IAsyncEnumerable<IFileReference> GetFiles(CloudFileDirectory dir, string[] pathPattern,
            CloudFile[] files)
        {
            var filePatterns = pathPattern.Select(p => p.TrimStart('/')).Where(p => !p.Contains('/'));

            foreach (var pattern in filePatterns)
            {
                //if (pattern.Contains("*"))
                //{
                var rx = new Regex(Regex.Escape(pattern).Replace("\\*", ".*"));
                foreach (var file in files.Where(f => rx.IsMatch(f.Name)))
                {
                    var azFile = await AzureFile.FromCloudFile(file, this.downloadFilesOnceFound);
                    this.toDispose.Add(azFile);
                    yield return azFile;
                }

                //TODO: This would be more efficient for single files
                //}
                //else
                //{
                //    yield return await AzureFile.FromCloudFile(dir.GetFileReference(pattern), this.downloadFilesOnceFound);
                //}
            }
        }

        private static string getMsiToken(string resourceId)
        {
            var request = (HttpWebRequest)WebRequest.Create(
                "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=" + HttpUtility.UrlEncode(resourceId));
            request.Headers["Metadata"] = "true";
            request.Method = "GET";

            try
            {
                var response = (HttpWebResponse)request.GetResponse();
                using var streamResponse = new StreamReader(response.GetResponseStream());
                var stringResponse = streamResponse.ReadToEnd();
                var list = JsonConvert.DeserializeObject<Dictionary<string, string>>(stringResponse);
                return list["access_token"];
            }
            catch (Exception e)
            {
                var errorText =
                    $"{e.Message}\n\n{(e.InnerException != null ? e.InnerException.Message : "Acquire token failed")}";
                throw new AuthenticationException(errorText, e.InnerException);
            }
        }

        public virtual async ValueTask DisposeAsync()
        {
            await this.toDispose.DisposeAsync();
        }
    }

    public class AzureFile : IFileReference, IDisposable
    {
        private readonly CloudFile cloudFile;
        private string tmp;

        private AzureFile(CloudFile cloudFile)
        {
            this.cloudFile = cloudFile;
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

        public string FileReference => this.cloudFile.Uri.AbsoluteUri;

        public static async Task<AzureFile> FromCloudFile(CloudFile cloudFile, bool download)
        {
            var azf = new AzureFile(cloudFile);
            if (download) await azf.Download();

            return azf;
        }

       protected virtual async Task Download()
        {
            this.tmp = Path.GetTempFileName();
            await this.cloudFile.DownloadToFileAsync(this.tmp, FileMode.Create);
        }
    }

    public class AzureFileProviderOptions
    {
        public Dictionary<string, string[]> SharesToPaths { get; set; }
    }
}