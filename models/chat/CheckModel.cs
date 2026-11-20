#!/usr/bin/env dotnet
#:property TargetFramework=net11.0
#:property LangVersion=15.0
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:package Microsoft.ML.OnnxRuntimeGenAI@0.20.0

// Smoke test for the exported Qwen3-0.6B ONNX GenAI model:
//   1. Tool calling via Qwen's chat template and <tool_call> protocol
//   2. Structured JSON output via ONNX Runtime GenAI's json_schema guidance
//
// Usage (from the repository root):  dotnet run models/chat/CheckModel.cs

using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.ML.OnnxRuntimeGenAI;

var folder = Path.Combine(SourceDirectory(), "Qwen3-0.6B");
if (!File.Exists(Path.Combine(folder, "model.onnx")))
    throw new FileNotFoundException($"Model not found in {folder}. Run models/chat/PrepareModel.py first.");

using var model = new Model(folder);
using var tokenizer = new Tokenizer(model);


// ---------------------------------------------------------------------------------------------
// 1. Tool calling smoke test
// ---------------------------------------------------------------------------------------------

const string toolsJson = """
[
  {
    "type": "function",
    "function": {
      "name": "get_speaker",
      "description": "Get the biography for a conference speaker by name.",
      "parameters": {
        "type": "object",
        "properties": {
          "name": { "type": "string", "description": "The speaker's full name." }
        },
        "required": ["name"]
      }
    }
  }
]
""";

const string toolMessagesJson = """
[
  { "role": "system", "content": "Use tools to look up conference speakers." },
  { "role": "user", "content": "Who is Thandi Nkosi?" }
]
""";

var toolPrompt = tokenizer.ApplyChatTemplate(null!, toolMessagesJson, toolsJson, add_generation_prompt: true)
    + "<think>\n\n</think>\n\n";
var toolOutput = Generate(toolPrompt);
Console.WriteLine($"Tool response:       {toolOutput}");

const string openTag = "<tool_call>";
const string closeTag = "</tool_call>";
var start = toolOutput.IndexOf(openTag, StringComparison.Ordinal);
var end = toolOutput.IndexOf(closeTag, StringComparison.Ordinal);
if (start < 0 || end <= start)
    throw new InvalidOperationException($"Expected a <tool_call> block, got: {toolOutput}");

using var callJson = JsonDocument.Parse(toolOutput[(start + openTag.Length)..end].Trim());
var toolName = callJson.RootElement.GetProperty("name").GetString();
var speakerArg = callJson.RootElement.GetProperty("arguments").GetProperty("name").GetString();
var toolResult = (toolName, speakerArg) switch
{
    ("get_speaker", { } name) => $"Thandi Nkosi is a principal mobile architect speaking about {name}.",
    _ => throw new InvalidOperationException($"Unexpected tool call: {toolName}({speakerArg})")
};
Console.WriteLine($"Tool invoked:        {toolName}(name: \"{speakerArg}\") -> {toolResult}");

if (!string.Equals(speakerArg, "Thandi Nkosi", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException($"Expected speaker 'Thandi Nkosi', got '{speakerArg}'.");


// ---------------------------------------------------------------------------------------------
// 2. Structured output (JSON schema) smoke test
// ---------------------------------------------------------------------------------------------

const string jsonSchema = """
{
  "type": "object",
  "properties": {
    "topic": { "type": "string", "enum": ["AI", "Mobile", "Web"] }
  },
  "required": ["topic"],
  "additionalProperties": false
}
""";

const string schemaMessagesJson = """
[
  { "role": "user", "content": "Classify this session into one topic: Building Cross-Platform Apps with .NET MAUI" }
]
""";

var schemaPrompt = tokenizer.ApplyChatTemplate(null!, schemaMessagesJson, null!, add_generation_prompt: true)
    + "<think>\n\n</think>\n\n";
var structuredOutput = Generate(schemaPrompt, jsonSchema);
Console.WriteLine($"Structured output:   {structuredOutput}");

using var structuredJson = JsonDocument.Parse(structuredOutput);
var topic = structuredJson.RootElement.GetProperty("topic").GetString();
if (topic != "Mobile")
    throw new InvalidOperationException($"Expected topic 'Mobile', got '{topic}'.");

Console.WriteLine($"Model checks passed for {folder}");


string Generate(string prompt, string? schema = null)
{
    using var tokens = tokenizer.Encode(prompt);
    using var parameters = new GeneratorParams(model);
    parameters.SetSearchOption("max_length", tokens[0].Length + 128);
    parameters.SetSearchOption("do_sample", false);
    if (schema is not null)
        parameters.SetGuidance("json_schema", schema);

    using var generator = new Generator(model, parameters);
    generator.AppendTokenSequences(tokens);

    var outputIds = new List<int>();
    while (!generator.IsDone())
    {
        generator.GenerateNextToken();
        var sequence = generator.GetSequence(0);
        if (sequence.Length <= tokens[0].Length + outputIds.Count)
            break;
        outputIds.Add(sequence[^1]);
    }

    return tokenizer.Decode(outputIds.ToArray()).Trim();
}

static string SourceDirectory([CallerFilePath] string file = "") => Path.GetDirectoryName(file)!;
