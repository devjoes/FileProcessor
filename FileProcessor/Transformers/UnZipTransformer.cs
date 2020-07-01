using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using FileProcessor.Providers;

namespace FileProcessor.Transformers
{
    public class UnZipTransformer : IAsyncEnumerableStep<IFileReference, IFileReference>, IDisposable
    {
        private string tmpDir;
        private readonly CompositeDisposable toDispose;

        public UnZipTransformer()
        {
            this.toDispose = new CompositeDisposable();
        }

        public virtual async IAsyncEnumerable<IFileReference> Execute(IFileReference input)
        {
            if (input == null)
            {
                yield break;
            }
            var zipFi = await input.GetLocalFileInfo();
            using var zip = ZipFile.OpenRead(zipFi.FullName);
            this.tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(this.tmpDir);
            foreach (var entry in zip.Entries)
            {
                var str = entry.Open();
                var path = Path.Combine(this.tmpDir, entry.Name);
                await using var file = File.Open(path, FileMode.Create, FileAccess.Write);
                await str.CopyToAsync(file);
                file.Close();
                var localFile = new LocalFile(path);
                this.toDispose.Add(localFile);
                yield return localFile;
            }
        }

        public virtual void Dispose()
        {
            this.toDispose.Dispose();
            if (this.tmpDir != null)
            {
                try
                {
                    Directory.Delete(this.tmpDir, true);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}