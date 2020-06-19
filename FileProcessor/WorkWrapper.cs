using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FileProcessor
{
    public interface IWorkWrapper
    {
        int Index { get; set; }
        //<string, string> MetaData { get; set; }
    }
    public class WorkWrapper<T>:IWorkWrapper
    {
        public WorkWrapper()
        {
            //this.MetaData = new Dictionary<string, string>();
        }

        public WorkWrapper(T work, IWorkWrapper previous)
        {
            this.Work = work;
            this.Index = previous.Index + 1;
            //var metaData = previous.MetaData;
            //if (this.Work is IHasPropagatingMetaData withMetaData)
            //{
            //    var rx = new Regex("\\d+_");
            //    var toAdd = withMetaData.MetaData.Keys.Where(k => !rx.IsMatch(k));
            //    foreach (var k in toAdd)
            //    {
            //        metaData.Add($"{this.Index}_{k}", withMetaData.MetaData[k]);
            //    }
            //    // This Work is finished for this step so there is no harm in this + helps with testing and frees up the reference for GC
            //    withMetaData.MetaData = metaData;
            //}

            //this.MetaData = metaData;
        }

        public T Work { get; set; }
        public TaskCompletionSource<object> CompletionSource { get; set; }
        public int Index { get; set; }
        //public Dictionary<string,string> MetaData { get; set; }

        public static WorkWrapper<object> NoOperation<TIn>(WorkWrapper<TIn> consumed)
        {
            return new WorkWrapper<object>
            {
                Work = default,
                CompletionSource = consumed.CompletionSource,
                //MetaData = consumed.MetaData
            };
        }
    }
}