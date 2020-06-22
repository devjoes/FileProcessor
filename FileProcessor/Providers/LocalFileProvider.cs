using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileProcessor.Providers
{
    public class LocalFileProvider : IStep<LocalFileProviderOptions, IEnumerable<IFileReference>>
    {
        public IEnumerable<IFileReference> Execute(LocalFileProviderOptions input)
        {
            return Directory.GetFiles(input.Path).Select(f => new LocalFile(f, false));
        }
    }

    public class LocalFile : IFileReference, IDisposable
    {
        private readonly bool deleteOnDispose;
        private string path;

        public LocalFile(string path, bool deleteOnDispose = true)
        {
            this.deleteOnDispose = deleteOnDispose;
            this.path = path;
            this.FileReference = path;
        }

        public Task<FileInfo> GetLocalFileInfo()
        {
            return Task.FromResult(new FileInfo(this.path));
        }

        public string FileReference { get; set; }

        public void Dispose()
        {
            if (this.path == null || !this.deleteOnDispose)
                return;

            var toDelete = this.path;
            try
            {
                File.Delete(toDelete);
                this.path = null;
            }
            catch
            {
                // ignore
            }
        }
    }

    public class LocalFileProviderOptions
    {
        public string Path { get; set; }
    }
}