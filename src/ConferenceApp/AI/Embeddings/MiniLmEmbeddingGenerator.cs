using System.Numerics.Tensors;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace ConferenceApp.AI;

// Local 384-dimension sentence embedding generator powered by the bundled all-MiniLM-L6-v2 ONNX model,
// Microsoft.ML.Tokenizers BertTokenizer, and SIMD TensorPrimitives pooling and L2 normalization.
public sealed class MiniLmEmbeddingGenerator(ILogger<MiniLmEmbeddingGenerator> logger)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    private const string ModelId = "all-MiniLM-L6-v2";
    private const int Dimensions = 384;
    private const int MaximumTokens = 256;

    // Lazy-load the bundled ONNX model and WordPiece vocabulary once on first use.
    private readonly Lazy<Task<(InferenceSession Session, BertTokenizer Tokenizer)>> model =
        new(() => LoadModelAsync(logger));

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var (session, tokenizer) = await model.Value;
        var embeddings = new GeneratedEmbeddings<Embedding<float>>();

        foreach (var text in values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1. Tokenize text into WordPiece token IDs (with [CLS] and [SEP] tokens).
            var ids = tokenizer.EncodeToIds(text, MaximumTokens, addSpecialTokens: true, out _, out _);
            var count = ids.Count;
            int[] shape = [1, count];

            // 2. Run the ONNX transformer model; last_hidden_state has shape [1, tokens, 384].
            using var outputs = session.Run(
            [
                NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(ids.Select(id => (long)id).ToArray(), shape)),
                NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(Enumerable.Repeat(1L, count).ToArray(), shape)),
                NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(new long[count], shape))
            ]);

            // 3. Pool token vectors and L2-normalize to a unit vector using SIMD TensorPrimitives.
            var hidden = outputs.Single(output => output.Name == "last_hidden_state").AsEnumerable<float>().ToArray();
            var vector = new float[Dimensions];
            for (var token = 0; token < count; token++)
                TensorPrimitives.Add(vector, hidden.AsSpan(token * Dimensions, Dimensions), vector);

            TensorPrimitives.Divide(vector, TensorPrimitives.Norm(vector), vector);
            embeddings.Add(new(vector));
        }

        return embeddings;
    }

    private static async Task<(InferenceSession, BertTokenizer)> LoadModelAsync(ILogger logger)
    {
        logger.LogInformation("[AI] Loading ONNX embedding model '{ModelId}' ({Dimensions} dimensions)...", ModelId, Dimensions);
        await using var vocabulary = await FileSystem.OpenAppPackageFileAsync($"models/{ModelId}/vocab.txt");
        var tokenizer = BertTokenizer.Create(vocabulary);

        await using var source = await FileSystem.OpenAppPackageFileAsync($"models/{ModelId}/model.onnx");
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer);

        return (new InferenceSession(buffer.ToArray()), tokenizer);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
        if (model.IsValueCreated && model.Value.IsCompletedSuccessfully)
            model.Value.Result.Session.Dispose();
    }
}
