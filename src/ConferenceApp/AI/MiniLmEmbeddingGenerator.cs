using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace ConferenceApp.AI;

public sealed class MiniLmEmbeddingGenerator(ILogger<MiniLmEmbeddingGenerator> logger)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    private const string ModelId = "all-MiniLM-L6-v2";
    private const int Dimensions = 384;
    private const int MaximumTokens = 256;
    private static readonly EmbeddingGeneratorMetadata Metadata =
        new("onnx", defaultModelId: ModelId, defaultModelDimensions: Dimensions);
    private readonly SemaphoreSlim gate = new(1, 1);
    private (InferenceSession Session, BertTokenizer Tokenizer)? model;
    private bool disposed;

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (options?.Dimensions is int dimensions && dimensions != Dimensions)
            throw new NotSupportedException($"MiniLM produces {Dimensions}-dimensional embeddings.");
        if (options?.ModelId is string modelId && modelId != ModelId)
            throw new NotSupportedException($"Only the packaged {ModelId} model is available.");

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return await Task.Run(async () =>
            {
                try
                {
                    if (model is null)
                    {
                        logger.LogInformation("[AI] Loading ONNX embedding model '{ModelId}' ({Dimensions} dimensions)...", ModelId, Dimensions);
                        model = await LoadModelAsync(cancellationToken).ConfigureAwait(false);
                    }
                    var (session, tokenizer) = model.Value;
                    var embeddings = new GeneratedEmbeddings<Embedding<float>>();
                    foreach (var text in values)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ArgumentNullException.ThrowIfNull(text);
                        var ids = tokenizer.EncodeToIds(text, MaximumTokens, addSpecialTokens: true, out _, out _);
                        var count = ids.Count;
                        int[] shape = [1, count];
                        using var outputs = session.Run(
                        [
                            NamedOnnxValue.CreateFromTensor("input_ids",
                                new DenseTensor<long>(ids.Select(id => (long)id).ToArray(), shape)),
                            NamedOnnxValue.CreateFromTensor("attention_mask",
                                new DenseTensor<long>(Enumerable.Repeat(1L, count).ToArray(), shape)),
                            NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(new long[count], shape))
                        ]);
                        cancellationToken.ThrowIfCancellationRequested();
                        var hidden = outputs.Single(output => output.Name == "last_hidden_state").AsTensor<float>();

                        // Mean pooling + L2 normalization
                        var vector = new float[Dimensions];
                        for (var token = 0; token < count; token++)
                            for (var dimension = 0; dimension < Dimensions; dimension++)
                                vector[dimension] += hidden[0, token, dimension] / count;
                        var length = MathF.Sqrt(vector.Sum(value => value * value));
                        if (!float.IsFinite(length) || length <= 0)
                            throw new InvalidOperationException("The packaged MiniLM model returned an invalid embedding.");
                        for (var dimension = 0; dimension < Dimensions; dimension++)
                            vector[dimension] /= length;
                        embeddings.Add(new(vector) { ModelId = ModelId });
                    }
                    return embeddings;
                }
                catch (Exception exception) when (exception is OnnxRuntimeException or DllNotFoundException or
                    EntryPointNotFoundException or BadImageFormatException)
                {
                    throw new InvalidOperationException("Couldn't run the packaged MiniLM sentence model on this device.", exception);
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<(InferenceSession, BertTokenizer)> LoadModelAsync(CancellationToken cancellationToken)
    {
        await using var vocabulary = await FileSystem.OpenAppPackageFileAsync($"models/{ModelId}/vocab.txt").ConfigureAwait(false);
        var tokenizer = BertTokenizer.Create(vocabulary);
        await using var source = await FileSystem.OpenAppPackageFileAsync($"models/{ModelId}/model.onnx").ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        using var options = new SessionOptions { IntraOpNumThreads = 1 };
        return (new InferenceSession(buffer.ToArray(), options), tokenizer);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null)
            return null;
        return serviceType == typeof(EmbeddingGeneratorMetadata) ? Metadata :
            serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
        gate.Wait();
        try
        {
            if (disposed)
                return;
            disposed = true;
            model?.Session.Dispose();
        }
        finally
        {
            gate.Release();
        }
        GC.SuppressFinalize(this);
    }
}
