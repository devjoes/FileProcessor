using System.Collections.Generic;
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

        public async IAsyncEnumerable<T> Execute(IFileReference input)
        {
            var fi = await input.GetLocalFileInfo();
            using var streamReader = fi.OpenText();
            var reader = new JsonTextReader(streamReader);
            var serializer = this.options.SerializerSettings == null
                ? new JsonSerializer()
                : JsonSerializer.Create(this.options.SerializerSettings);
            await foreach (var item in this.ReadJson(reader, serializer)) yield return item;
        }

        protected virtual async IAsyncEnumerable<T> ReadJson(JsonTextReader reader, JsonSerializer serializer)
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