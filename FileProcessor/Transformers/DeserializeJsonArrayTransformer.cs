using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
            string state = "";
            if (reader is IJsonTextReaderWithExposedTextReader exposedTextReader)
            {
                streamReader = exposedTextReader.Reader as StreamReader;
                state= exposedTextReader.ReaderState;
            }

            int lineNumber=-1, linePos=-1, depth=-1;
            if (reader is JsonTextReader textReader)
            {
                lineNumber = textReader.LineNumber;
                linePos = textReader.LinePosition;
                depth = textReader.Depth;
            }

            void ThrowJsonEx(string msg, JsonReaderException ex)
            {
                throw new Exception(msg +
                                    "\nPath: " + reader.Path +
                                    "\nState: " + state +
                                    $"\nTextInfo: {lineNumber} {linePos} {depth}"+
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

    // TODO: I think this was just for debugging and can be removed
    public interface IJsonTextReaderWithExposedTextReader
    {
        TextReader Reader { get; }
        string ReaderState { get; }
    }
    public class JsonTextReaderWithExposedTextReader : JsonTextReader, IJsonTextReaderWithExposedTextReader
    {
        public JsonTextReaderWithExposedTextReader(TextReader reader) : base(reader)
        {
            this.Reader = reader;
        }

        public TextReader Reader { get; }
        public string ReaderState => this.CurrentState.ToString();
    }
}