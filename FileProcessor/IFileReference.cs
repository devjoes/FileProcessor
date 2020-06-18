using System.IO;
using System.Threading.Tasks;

namespace FileProcessor
{
    public interface IFileReference
    {
        string FileReference { get; }
        Task<FileInfo> GetLocalFileInfo();
    }
}