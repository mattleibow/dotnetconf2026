#!/usr/bin/env dotnet
#:property TargetFramework=net11.0
#:property LangVersion=15.0
#:property Nullable=enable
#:package Microsoft.ML.OnnxRuntime@1.31.1
#:package Microsoft.ML.Tokenizers@2.0.1

// Downloads the all-MiniLM-L6-v2 sentence-embedding model that the app uses for on-device
// semantic search where there's no built-in embedding model, then checks that it works:
//
//   all-MiniLM-L6-v2/model.onnx    int8-quantized ONNX model (~22 MB, runs on any CPU)
//   all-MiniLM-L6-v2/vocab.txt     WordPiece vocabulary for BertTokenizer
//
// Usage (from the repository root):  dotnet run models/DownloadModels.cs
//
// Files are pinned to one Hugging Face revision and checked against their SHA-256 hashes,
// so every run produces the same bytes.

using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

const string repository = "sentence-transformers/all-MiniLM-L6-v2";
const string revision = "1110a243fdf4706b3f48f1d95db1a4f5529b4d41";

// The int8 file is published as "arm64" but is byte-identical to the "avx512" one, so it isn't
// tied to a CPU type.
(string Source, string Target, string Sha256)[] files =
[
    ("onnx/model_qint8_arm64.onnx", "model.onnx", "4278337fd0ff3c68bfb6291042cad8ab363e1d9fbc43dcb499fe91c871902474"),
    ("vocab.txt", "vocab.txt", "07eced375cec144d27c900241f3e339478dec958f92fddbc551f295c992038a3"),
];

var folder = Path.Combine(SourceDirectory(), "all-MiniLM-L6-v2");
Directory.CreateDirectory(folder);


// ---------------------------------------------------------------------------------------------
// Download
// ---------------------------------------------------------------------------------------------

using (var http = new HttpClient())
{
    foreach (var (source, target, sha256) in files)
    {
        Console.WriteLine($"Downloading {source} -> {target}");
        var bytes = await http.GetByteArrayAsync($"https://huggingface.co/{repository}/resolve/{revision}/{source}");

        var actual = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (actual != sha256)
            throw new InvalidDataException($"{source} has SHA-256 {actual}, expected {sha256}.");

        await File.WriteAllBytesAsync(Path.Combine(folder, target), bytes);
    }
}


// ---------------------------------------------------------------------------------------------
// Smoke test: a related sentence should score higher than an unrelated one
// ---------------------------------------------------------------------------------------------

// The model expects lowercase input; BertTokenizer lowercases by default.
var tokenizer = BertTokenizer.Create(Path.Combine(folder, "vocab.txt"));
using var session = new InferenceSession(Path.Combine(folder, "model.onnx"));

var query = Embed("saving money on AI");
var related = Embed("The hidden cost of just calling the LLM: architecture patterns for cost-efficient GenAI");
var unrelated = Embed("Lunch and networking");

var relatedScore = TensorPrimitives.CosineSimilarity(query, related);
var unrelatedScore = TensorPrimitives.CosineSimilarity(query, unrelated);
Console.WriteLine($"Embedding size: {query.Length}");
Console.WriteLine($"Similarity to a related talk:   {relatedScore:F3}");
Console.WriteLine($"Similarity to an unrelated one: {unrelatedScore:F3}");

if (relatedScore <= unrelatedScore)
    throw new InvalidOperationException("The model doesn't rank the related sentence higher; something is wrong.");

Console.WriteLine($"Model ready in {folder}");


// Tokenize, run the model, then pool the token vectors and L2-normalize with TensorPrimitives.
// The app's ONNX embedding generator does the same thing.
float[] Embed(string text)
{
    var ids = tokenizer.EncodeToIds(text); // Adds the [CLS] and [SEP] tokens.
    var count = ids.Count;
    int[] shape = [1, count];

    using var outputs = session.Run(
    [
        NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(ids.Select(id => (long)id).ToArray(), shape)),
        NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(Enumerable.Repeat(1L, count).ToArray(), shape)),
        NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(new long[count], shape)),
    ]);

    // last_hidden_state has the shape [1, tokens, 384].
    var hidden = outputs.Single(output => output.Name == "last_hidden_state").AsEnumerable<float>().ToArray();
    var vector = new float[384];

    for (var token = 0; token < count; token++)
        TensorPrimitives.Add(vector, hidden.AsSpan(token * 384, 384), vector);

    TensorPrimitives.Divide(vector, TensorPrimitives.Norm(vector), vector);
    return vector;
}

static string SourceDirectory([CallerFilePath] string file = "") => Path.GetDirectoryName(file)!;
