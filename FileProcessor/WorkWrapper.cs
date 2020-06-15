using System.Collections;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;

namespace FileProcessor
{
    public class WorkWrapper<T>
    {
        public T Work { get; set; }
        public TaskCompletionSource<object> CompletionSource { get; set; }
    }
}
