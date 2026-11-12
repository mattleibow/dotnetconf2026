using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace ConferenceApp.AI;

public sealed class MiniLmEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private const string ModelId = "all-MiniLM-L6-v2";
    private const int Dimensions = 384;
    private const int MaximumTokens = 256;

    private readonly Lazy<Task<(InferenceSession Session, BertTokenizer Tokenizer)>> model;

    public MiniLmEmbeddingGenerator(ILogger<MiniLmEmbeddingGenerator> logger)
    {
        model = new(() => LoadModelAsync(logger));
    }

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
            var ids = tokenizer.EncodeToIds(text, MaximumTokens, addSpecialTokens: true, out _, out _);
            var count = ids.Count;
            int[] shape = [1, count];

            using var outputs = session.Run(
            [
                NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(ids.Select(id => (long)id).ToArray(), shape)),
                NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(Enumerable.Repeat(1L, count).ToArray(), shape)),
                NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(new long[count], shape))
            ]);

            var hidden = outputs.Single(output => output.Name == "last_hidden_state").AsTensor<float>();
            var vector = new float[Dimensions];
            for (var token = 0; token < count; token++)
                for (var dimension = 0; dimension < Dimensions; dimension++)
                    vector[dimension] += hidden[0, token, dimension] / count;

            var length = MathF.Sqrt(vector.Sum(value => value * value));
            for (var dimension = 0; dimension < Dimensions; dimension++)
                vector[dimension] /= length;

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
