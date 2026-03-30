using System.Collections.Frozen;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace JD.AI.Workflows;

/// <summary>
/// ML.NET-based intent classifier that loads a pre-trained model from a <c>.zip</c> file
/// and classifies prompts as workflow or conversation intent.
/// Implements <see cref="IPromptIntentClassifier"/> so it is a drop-in replacement for
/// <see cref="TfIdfIntentClassifier"/>.
/// </summary>
public sealed class MlNetIntentClassifier : IPromptIntentClassifier, IHotSwappableClassifier, IDisposable
{
    /// <summary>
    /// ML.NET input schema: prompt text + integer label (0=conversation, 1=workflow).
    /// </summary>
    internal sealed partial class PromptInput
    {
        [LoadColumn(0)]
        public string Prompt { get; set; } = "";

        [LoadColumn(1)]
        public bool IsWorkflow { get; set; }
    }

    /// <summary>
    /// ML.NET output schema: predicted label, score, and probability.
    /// </summary>
    internal sealed partial class PromptPrediction
    {
        [ColumnName("PredictedLabel")]
        public bool PredictedLabel { get; set; }

        public float Score { get; set; }

        public float Probability { get; set; }
    }

    private readonly string _modelPath;
    private readonly MLContext _ml;
    private ITransformer? _model;
    private PredictionEngine<PromptInput, PromptPrediction>? _engine;
    private readonly System.Threading.Lock _lock = new();

    // ── Thresholds ─────────────────────────────────────────────────────
    // Matches TfIdfIntentClassifier threshold (0.40) so behaviour is consistent
    // during the A/B window where both classifiers run.
    private const double WorkflowThreshold = 0.40;

    /// <summary>
    /// Creates a classifier that loads its trained model from <paramref name="modelPath"/>.
    /// </summary>
    /// <param name="modelPath">Path to the ML.NET <c>.zip</c> model file.</param>
    public MlNetIntentClassifier(string modelPath)
    {
        _modelPath = modelPath ?? throw new ArgumentNullException(nameof(modelPath));
        _ml = new MLContext(seed: 42);
        ReloadModel();
    }

    /// <inheritdoc/>
    public IntentClassification Classify(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return new IntentClassification(IsWorkflow: false, Confidence: 0.0, SignalWords: []);

        lock (_lock)
        {
            if (_engine is null)
                return new IntentClassification(IsWorkflow: false, Confidence: 0.0, SignalWords: []);

            var input = new PromptInput { Prompt = prompt };
            var prediction = _engine.Predict(input);

            // Probability is calibrated [0, 1]; use it as confidence
            var confidence = Math.Clamp(prediction.Probability, 0.0, 1.0);
            var isWorkflow = confidence >= WorkflowThreshold;

            // ML.NET does not provide per-token signal attribution.
            // Return the prediction score as signal strength indicator.
            return new IntentClassification(
                IsWorkflow: isWorkflow,
                Confidence: confidence,
                SignalWords: []);
        }
    }

    /// <summary>
    /// Reloads the model from disk, replacing the live prediction engine atomically.
    /// Thread-safe. Called by <see cref="IIntentClassifierHotSwapper"/> on file change.
    /// </summary>
    public void ReloadModel()
    {
        lock (_lock)
        {
            if (!File.Exists(_modelPath))
                return;

            using var stream = File.OpenRead(_modelPath);
            _model = _ml.Model.Load(stream, out _);
            _engine = _ml.Model.CreatePredictionEngine<PromptInput, PromptPrediction>(_model);
        }
    }

    void IHotSwappableClassifier.Reload() => ReloadModel();

    /// <summary>
    /// Returns the model file path being used.
    /// </summary>
    public string ModelPath => _modelPath;

    /// <summary>
    /// Returns true when a model has been successfully loaded.
    /// </summary>
    public bool IsModelLoaded => _engine is not null;

    public void Dispose()
    {
        _engine?.Dispose();
        (_model as IDisposable)?.Dispose();
    }
}
