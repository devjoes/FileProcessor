using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace FileProcessor.Transformers
{
    public class CsvToObjectTransformer<T> : IAsyncEnumerableStep<IFileReference, T>
    {
        private readonly CsvConfiguration csvConfig;

        public CsvToObjectTransformer(CsvConfiguration csvConfig = null)
        {
            this.csvConfig = csvConfig ?? new CsvConfiguration(CultureInfo.InvariantCulture);
        }

        public async IAsyncEnumerable<T> Execute(IFileReference input)
        {
            if (input == null)
            {
                yield break;
            }
            await using var stream = (await input.GetLocalFileInfo()).OpenRead();
            using var reader = new StreamReader(stream);
            var csv = new CsvReader(reader, this.csvConfig);
            await foreach (var record in csv.GetRecordsAsync<T>())
            {
                yield return record;
            }
        }
    }
}
