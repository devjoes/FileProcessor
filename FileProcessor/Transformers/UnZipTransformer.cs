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

        public async IAsyncEnumerable<IFileReference> Execute(IFileReference input)
        {
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
                yield return new LocalFile(path);
            }
        }

        public void Dispose()
        {
            if (this.tmpDir != null) Directory.Delete(this.tmpDir, true);
        }
    }
}