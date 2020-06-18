using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FileProcessor
{

    public interface IStep<in TIn, out TOut>
    {
        TOut Execute(TIn input);
    }
    public interface IAsyncStep<in TIn, TOut>
    {
        Task<TOut> Execute(TIn input);
    }
    public interface IAsyncEnumerableStep<in TIn, out TOut>
    {
        IAsyncEnumerable<TOut> Execute(TIn input);
    }

    public class StepOptions
    {
        public int Parallelism { get; set; } = 1;
        public int BufferCapacity { get; set; } = 10;
    }
}
