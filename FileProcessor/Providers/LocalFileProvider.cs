using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileProcessor.Providers
{
    public class LocalFileProvider : IStep<LocalFileProviderOptions, IEnumerable<IFileReference>>
    {
        public virtual IEnumerable<IFileReference> Execute(LocalFileProviderOptions input)
        {
            if (input.Path.Contains("*"))
            {
                var components = input.Path.Split(Path.DirectorySeparatorChar);
                var searchIn = string.Join(Path.DirectorySeparatorChar, components.TakeWhile(c => !c.Contains("*")));
                var searchFor = input.Path.Substring(searchIn.Length).Trim(Path.DirectorySeparatorChar);
                return Directory.GetFiles(searchIn, searchFor, SearchOption.TopDirectoryOnly)
                    .Select(f => new LocalFile(f, false));
            }

            if (File.Exists(input.Path))
            {
                return new[] {new LocalFile(input.Path)};
            }

            return Directory.GetFiles(input.Path).Select(f => new LocalFile(f, false));
        }
    }

    public class LocalFile : IFileReference, IDisposable
    {
        protected readonly bool DeleteOnDispose;
        protected string Path;

        public LocalFile(string path, bool deleteOnDispose = true)
        {
            this.DeleteOnDispose = deleteOnDispose;
            this.Path = path;
            this.FileReference = path;
        }

        public virtual Task<FileInfo> GetLocalFileInfo()
        {
            return Task.FromResult(new FileInfo(this.Path));
        }

        public string FileReference { get; set; }

        public virtual void Dispose()
        {
            if (this.Path == null || !this.DeleteOnDispose)
                return;

            var toDelete = this.Path;
            try
            {
                File.Delete(toDelete);
                this.Path = null;
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