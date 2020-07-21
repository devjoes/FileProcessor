using System;
using System.Collections.Generic;
using System.IO;
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

        //todo: put back
        protected virtual async Task<JsonReader> GetReader(IFileReference input)
        {
            var fi = await input.GetLocalFileInfo();
            var streamReader = fi.OpenText();
            return new JsonTextReaderWithExposedTextReader(streamReader);
        }

        protected virtual async IAsyncEnumerable<T> ReadJson(JsonReader reader, JsonSerializer serializer)
        {
            var eof = false;

            StreamReader streamReader = null;
            if (reader is IJsonTextReaderWithExposedTextReader exposedTextReader)
            {
                streamReader = exposedTextReader.Reader as StreamReader;
            }

            void ThrowJsonEx(string msg, JsonReaderException ex)
            {
                throw new Exception(msg + "\n" +
                                    "Path: " + reader.Path +
                                    (streamReader == null ? string.Empty
                                    : "\nPosition: " + streamReader.BaseStream.Position +
                                     "\nCanRead: " + streamReader.BaseStream.CanRead)
                    , ex);
            }
            try
            {
                while (reader.Path != this.options.JsonPath && !eof) eof = !await reader.ReadAsync();
            }
            catch (JsonReaderException ex)
            {
                ThrowJsonEx("Error skipping JSON", ex);
            }

            while (!eof)
            {
                if (reader.TokenType == JsonToken.StartObject && reader.Path.StartsWith(this.options.JsonPath))
                {
                    var obj = serializer.Deserialize<T>(reader);
                    yield return obj;
                }

                try
                {
                    eof = !await reader.ReadAsync();
                }
                catch (JsonReaderException ex)
                {
                    ThrowJsonEx("Error parsing JSON", ex);
                }
            }

        }
    }

    public class DeserializeJsonArrayTransformerOptions
    {
        public string JsonPath { get; set; } = string.Empty;
        public JsonSerializerSettings SerializerSettings { get; set; } = null;
    }

    public interface IJsonTextReaderWithExposedTextReader
    {
        TextReader Reader { get; }
    }
    public class JsonTextReaderWithExposedTextReader : JsonTextReader, IJsonTextReaderWithExposedTextReader
    {
        public JsonTextReaderWithExposedTextReader(TextReader reader) : base(reader)
        {
            this.Reader = reader;
        }

        public TextReader Reader { get; }
    }
}