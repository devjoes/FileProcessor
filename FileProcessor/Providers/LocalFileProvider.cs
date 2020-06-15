using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileProcessor.Providers
{
    public class LocalFileProvider:IStep<LocalFileProviderOptions, IEnumerable<IFileReference>>
    {
        public IEnumerable<IFileReference> Execute(LocalFileProviderOptions input)
        {
            return Directory.GetFiles(input.Path).Select(f => new LocalFile(f));
        }
    }
    
    public class LocalFile:IFileReference
    {
        public LocalFile(string path)
        {
            this.FileReference = path;
        }

        public Task<FileInfo> GetLocalFileInfo()=> Task.FromResult(new FileInfo(this.FileReference));

        public string FileReference { get; }
    }

    public class LocalFileProviderOptions
    {
        public string Path { get; set; }
    }
}
