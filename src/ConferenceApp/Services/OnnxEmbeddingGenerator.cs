using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace ConferenceApp.Services;

public sealed class OnnxEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private const string AssetFolder = "models/all-MiniLM-L6-v2/";
    private static readonly EmbeddingGeneratorMetadata Metadata = new("ONNX Runtime", null, "all-MiniLM-L6-v2", 384);
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
                using var vocabulary = await FileSystem.OpenAppPackageFileAsync(AssetFolder + "vocab.txt");
                var loadedTokenizer = BertTokenizer.Create(vocabulary);
                using var model = await FileSystem.OpenAppPackageFileAsync(AssetFolder + "model.onnx");
                using var bytes = new MemoryStream();
                await model.CopyToAsync(bytes, cancellationToken);
                session = new InferenceSession(bytes.ToArray());
                tokenizer = loadedTokenizer;
            }

            var embeddings = new GeneratedEmbeddings<Embedding<float>>();
            foreach (var text in values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var ids = tokenizer!.EncodeToIds(text).ToArray();
                if (ids.Length > 256)
                    ids = [.. ids.Take(255), ids[^1]];
                int[] shape = [1, ids.Length];
                using var outputs = session.Run(
                [
                    NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(ids.Select(id => (long)id).ToArray(), shape)),
                    NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(Enumerable.Repeat(1L, ids.Length).ToArray(), shape)),
                    NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(new long[ids.Length], shape))
                ]);
                var hidden = outputs.Single(output => output.Name == "last_hidden_state").AsTensor<float>();
                var vector = new float[hidden.Dimensions[2]];
                for (var token = 0; token < ids.Length; token++)
                    for (var dimension = 0; dimension < vector.Length; dimension++)
                        vector[dimension] += hidden[0, token, dimension] / ids.Length;

                var length = MathF.Sqrt(vector.Sum(value => value * value));
                if (length == 0 || !float.IsFinite(length))
                    throw new InvalidOperationException("The ONNX model produced an invalid embedding.");
                for (var i = 0; i < vector.Length; i++)
                    vector[i] /= length;
                embeddings.Add(new Embedding<float>(vector));
            }
            return embeddings;
        }
        finally
        {
            gate.Release();
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType == typeof(EmbeddingGeneratorMetadata) ? Metadata :
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
        session?.Dispose();
        gate.Dispose();
    }
}
