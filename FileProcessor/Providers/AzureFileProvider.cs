using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.File;
using Newtonsoft.Json;

namespace FileProcessor.Providers
{
    public class AzureFileProvider : IAsyncEnumerableStep<AzureFileProviderOptions, IFileReference>
    {
        private readonly bool downloadFilesOnceFound;

        public AzureFileProvider(string connectionString, bool authenticateWithMsi = false, bool downloadFilesOnceFound = true)
        {
            this.downloadFilesOnceFound = downloadFilesOnceFound;
            var storageAccount = authenticateWithMsi 
                ? new CloudStorageAccount(new StorageCredentials(new TokenCredential(getMsiToken("https://storage.azure.com/"))), true) 
                : CloudStorageAccount.Parse(connectionString);

            this.client = storageAccount.CreateCloudFileClient();
        }

        public AzureFileProvider(CloudFileClient client)
        {
            this.client = client;
        }

        private readonly CloudFileClient client;

        public async IAsyncEnumerable<IFileReference> Execute(AzureFileProviderOptions input)
        {
            foreach (string shareName in input.SharesToPaths.Keys)
            {
                var share = this.client.GetShareReference(shareName);
                var root = share.GetRootDirectoryReference();
                await foreach (var file in this.processDir(root, input.SharesToPaths[shareName].OrderBy(p => p).ToArray()))
                {
                    yield return file;
                }
            }
        }

        private async IAsyncEnumerable<IFileReference> processDir(CloudFileDirectory dir, string[] pathPatterns)
        {
            //TODO: Poss optimize this
            var dirContents = dir.ListFilesAndDirectories().ToArray();

            var files = dirContents.OfType<CloudFile>();
            await foreach (var file in this.getFiles(dir, pathPatterns, files.ToArray()))
            {
                yield return file;
            }

            //if (pathPatterns.Select(p => p.Trim('/')).Any(p => p.IndexOf('/') != p.LastIndexOf('/')))
            //{
            await foreach (var file in this.processSubDirs(pathPatterns, dirContents))
            {
                yield return file;
            }
            //}
        }

        private async IAsyncEnumerable<IFileReference> processSubDirs(string[] pathPatterns, IListFileItem[] dirContents)
        {
            var dirPatterns = pathPatterns
                .Select(p => p.Trim('/'))
                .Where(p => p.Contains('/'))
                .ToArray();
            if (!dirPatterns.Any())
            {
                yield break;
            }

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
                await foreach (var file in this.processDir(subDir, nextPatterns.ToArray()))
                {
                    yield return file;
                }
            }

        }

        private async IAsyncEnumerable<IFileReference> getFiles(CloudFileDirectory dir, string[] pathPattern, CloudFile[] files)
        {
            var filePatterns = pathPattern.Select(p => p.TrimStart('/')).Where(p => !p.Contains('/'));

            foreach (var pattern in filePatterns)
            {
                //if (pattern.Contains("*"))
                //{
                var rx = new Regex(Regex.Escape(pattern).Replace("\\*", ".*"));
                foreach (var file in files.Where(f => rx.IsMatch(f.Name)))
                {
                    yield return await AzureFile.FromCloudFile(file, this.downloadFilesOnceFound);
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
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=" + resourceId);
            request.Headers["Metadata"] = "true";
            request.Method = "GET";

            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                using StreamReader streamResponse = new StreamReader(response.GetResponseStream());
                string stringResponse = streamResponse.ReadToEnd();
                Dictionary<string, string> list = JsonConvert.DeserializeObject<Dictionary<string, string>>(stringResponse);
                return list["access_token"];
            }
            catch (Exception e)
            {
                string errorText =
                    $"{e.Message}\n\n{(e.InnerException != null ? e.InnerException.Message : "Acquire token failed")}";
                throw new AuthenticationException(errorText, e.InnerException);
            }
        }

    }

    public class AzureFile : IFileReference, IDisposable
    {
        private readonly CloudFile cloudFile;
        private string tmp = null;

        private AzureFile(CloudFile cloudFile)
        {
            this.cloudFile = cloudFile;
        }

        public static async Task<AzureFile> FromCloudFile(CloudFile cloudFile, bool download)
        {
            var azf = new AzureFile(cloudFile);
            if (download)
            {
                await azf.download();
            }

            return azf;
        }

        private async Task download()
        {
            this.tmp = Path.GetTempFileName();
            await this.cloudFile.DownloadToFileAsync(this.tmp, FileMode.Create);
        }

        public async Task<FileInfo> GetLocalFileInfo()
        {
            if (this.tmp == null)
            {
                await this.download();
            }

            return new FileInfo(this.tmp);
        }

        public string FileReference => this.cloudFile.Uri.AbsoluteUri;
        public void Dispose()
        {
            if (this.tmp != null)
            {
                // todo: call dispose
                File.Delete(this.tmp);
            }
        }
    }

    public class AzureFileProviderOptions
    {
        public Dictionary<string, string[]> SharesToPaths { get; set; }
    }
}
