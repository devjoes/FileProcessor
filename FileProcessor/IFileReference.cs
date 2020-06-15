using System.IO;
using System.Threading.Tasks;

namespace FileProcessor
{
    public interface IFileReference
    {
        Task<FileInfo> GetLocalFileInfo();
        string FileReference { get; }
    }
}