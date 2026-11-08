using System.Text.Json.Nodes;

namespace JsonDiff.Services;

/// <summary>Reads and parses a JSON file; abstracted so the view models can be tested without disk access.</summary>
public interface IJsonFileLoader
{
    /// <summary>Returns the parsed document (null for a document that is just <c>null</c>).</summary>
    /// <exception cref="System.IO.IOException">The file could not be read.</exception>
    /// <exception cref="System.Text.Json.JsonException">The file is not valid JSON.</exception>
    JsonNode? Load(string path);
}
