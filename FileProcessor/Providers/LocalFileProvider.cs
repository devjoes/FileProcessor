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

        public LocalFile(string path, bool deleteOnDispose = true)
        {
            this.deleteOnDispose = deleteOnDispose;
            this.FileReference = path;
        }

        public Task<FileInfo> GetLocalFileInfo()
        {
            return Task.FromResult(new FileInfo(this.FileReference));
        }

        public string FileReference { get; private set; }

        public void Dispose()
        {
            if (this.FileReference == null || !this.deleteOnDispose)
                return;

            var toDelete = this.FileReference;
            try
            {
                File.Delete(toDelete);
                this.FileReference = null;
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