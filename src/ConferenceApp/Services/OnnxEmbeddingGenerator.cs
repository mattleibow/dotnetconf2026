using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace ConferenceApp.Services;

public sealed class OnnxEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private const string ModelPath = "models/all-MiniLM-L6-v2/";
    private const int MaxTokens = 256;
    private static readonly EmbeddingGeneratorMetadata Metadata = new("ONNX Runtime", defaultModelId: "all-MiniLM-L6-v2", defaultModelDimensions: 384);
    private readonly SemaphoreSlim gate = new(1, 1);
    private BertTokenizer? tokenizer;
    private InferenceSession? session;

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (session is null)
            {
                using var vocabulary = await FileSystem.OpenAppPackageFileAsync(ModelPath + "vocab.txt");
                var loadedTokenizer = await BertTokenizer.CreateAsync(vocabulary, cancellationToken: cancellationToken);
                using var model = await FileSystem.OpenAppPackageFileAsync(ModelPath + "model.onnx");
                using var bytes = new MemoryStream();
                await model.CopyToAsync(bytes, cancellationToken);
                session = new InferenceSession(bytes.ToArray());
                tokenizer = loadedTokenizer;
            }

            return await Task.Run(() =>
            {
                var embeddings = new GeneratedEmbeddings<Embedding<float>>();
                foreach (var value in values)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    embeddings.Add(new Embedding<float>(Embed(value)));
                }
                return embeddings;
            }, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private float[] Embed(string text)
    {
        var ids = tokenizer!.EncodeToIds(text);
        // Keep the closing [SEP] when truncating to the model's training limit.
        var tokens = ids.Count <= MaxTokens ? ids.ToArray() : [.. ids.Take(MaxTokens - 1), ids[^1]];
        int[] shape = [1, tokens.Length];
        using var outputs = session!.Run(
        [
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(tokens.Select(id => (long)id).ToArray(), shape)),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(Enumerable.Repeat(1L, tokens.Length).ToArray(), shape)),
            NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(new long[tokens.Length], shape)),
        ]);
        var hidden = outputs.Single(output => output.Name == "last_hidden_state").AsTensor<float>();
        var vector = new float[hidden.Dimensions[2]];
        for (var token = 0; token < tokens.Length; token++)
            for (var dimension = 0; dimension < vector.Length; dimension++)
                vector[dimension] += hidden[0, token, dimension] / tokens.Length;

        var length = MathF.Sqrt(vector.Sum(value => value * value));
        if (length == 0 || !float.IsFinite(length))
            throw new InvalidOperationException("The ONNX model returned an invalid embedding.");
        for (var i = 0; i < vector.Length; i++)
            vector[i] /= length;
        return vector;
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is not null ? null : serviceType == typeof(EmbeddingGeneratorMetadata) ? Metadata
            : serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
        session?.Dispose();
        gate.Dispose();
    }
}
