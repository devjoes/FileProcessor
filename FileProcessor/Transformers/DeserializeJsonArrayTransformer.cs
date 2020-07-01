using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FileProcessor.Transformers
{
    public class DeserializeJsonArrayTransformer<T> : IAsyncEnumerableStep<IFileReference, T>
    {
        private readonly DeserializeJsonArrayTransformerOptions options;

        public DeserializeJsonArrayTransformer(DeserializeJsonArrayTransformerOptions options)
        {
            this.options = options;
        }

        public virtual async IAsyncEnumerable<T> Execute(IFileReference input)
        {
            if (input == null)
            {
                yield break;
            }
            using var reader = await this.GetReader(input);
            var serializer = this.options.SerializerSettings == null
                ? new JsonSerializer()
                : JsonSerializer.Create(this.options.SerializerSettings);
            await foreach (var item in this.ReadJson(reader, serializer))
            {
                yield return item;
            }
        }

        protected virtual async  Task<JsonReader> GetReader(IFileReference input)
        {
            var fi = await input.GetLocalFileInfo();
            var streamReader = fi.OpenText();
            return new JsonTextReader(streamReader);
        }

        protected virtual async IAsyncEnumerable<T> ReadJson(JsonReader reader, JsonSerializer serializer)
        {
            var eof = false;
            while (reader.Path != this.options.JsonPath && !eof) eof = !await reader.ReadAsync();

            while (!eof)
            {
                if (reader.TokenType == JsonToken.StartObject && reader.Path.StartsWith(this.options.JsonPath))
                {
                    var obj = serializer.Deserialize<T>(reader);
                    yield return obj;
                }

                eof = !await reader.ReadAsync();
            }
        }
    }

    public class DeserializeJsonArrayTransformerOptions
    {
        public string JsonPath { get; set; } = string.Empty;
        public JsonSerializerSettings SerializerSettings { get; set; } = null;
    }
}