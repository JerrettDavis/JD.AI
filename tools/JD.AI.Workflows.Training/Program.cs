using System.Diagnostics;
using JD.AI.Workflows;
using JD.AI.Workflows.Training;
using Microsoft.ML;
using Microsoft.ML.Data;
using Spectre.Console;

namespace JD.AI.Workflows.Training;

public static class Program
{
    // ML.NET schemas — kept private to the training tool
    private sealed class PromptInput
    {
        [LoadColumn(0)]
        public string Prompt = "";
        [LoadColumn(1)]
        public bool IsWorkflow;
    }

    private sealed class PromptPrediction
    {
        [ColumnName("PredictedLabel")]
        public bool PredictedLabel;
        public float Probability;
    }

    public static async Task<int> Main(string[] args)
    {
        AnsiConsole.Write(new FigletText("Intent Classifier Trainer").Color(Color.Blue));

        var benchmark = args.Contains("--benchmark") || args.Contains("-b");
        var generateOnly = args.Contains("--generate") || args.Contains("-g");
        var evaluateOnly = args.Contains("--evaluate") || args.Contains("-e");
        var dataArgIdx = Array.IndexOf(args, "--data");
        var dataPath = dataArgIdx >= 0 && dataArgIdx + 1 < args.Length ? args[dataArgIdx + 1] : null;
        var outputArgIdx = Array.IndexOf(args, "--output");
        var outputPath = outputArgIdx >= 0 && outputArgIdx + 1 < args.Length
            ? args[outputArgIdx + 1]
            : "../../src/JD.AI.Workflows/Models/intent_classifier.zip";

        if (benchmark)
        {
            RunBenchmark();
            return 0;
        }

        if (generateOnly)
        {
            dataPath ??= Path.Combine(Path.GetTempPath(), "intent_training_data.csv");
            GenerateData(dataPath, augmentPerSeed: 15);
            AnsiConsole.MarkupLine($"[green]Generated training data → {dataPath}[/]");
            return 0;
        }

        if (dataPath is null)
        {
            dataPath = Path.Combine(Path.GetTempPath(), "intent_training_data.csv");
            await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync(
                "Generating training data...", _ => { GenerateData(dataPath, 15); return Task.CompletedTask; });
            AnsiConsole.MarkupLine($"[dim]Generated {File.ReadAllLines(dataPath).Length - 1} prompts[/]");
        }

        if (evaluateOnly)
        {
            Evaluate(dataPath);
            return 0;
        }

        await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync(
            "Training model...", _ => { TrainModel(dataPath, outputPath); return Task.CompletedTask; });
        AnsiConsole.MarkupLine($"[green]Model saved → {outputPath}[/]");

        return 0;
    }

    private static void GenerateData(string path, int augmentPerSeed)
    {
        var prompts = TrainingDataGenerator.Generate(augmentPerSeed);
        TrainingDataGenerator.WriteCsv(prompts, path);
    }

    private static void TrainModel(string dataPath, string outputPath)
    {
        var ml = new MLContext(seed: 42);

        var prompts = TrainingDataGenerator.ReadCsv(dataPath);
        var mlData = ml.Data.LoadFromEnumerable(
            prompts.Select(p => new PromptInput { Prompt = p.Prompt, IsWorkflow = p.IsWorkflow }));

        var split = ml.Data.TrainTestSplit(mlData, testFraction: 0.2, seed: 42);

        // Pipeline: featurize text → SDCA logistic regression
        var pipeline = ml.Transforms.Text
            .FeaturizeText("Features", nameof(PromptInput.Prompt))
            .Append(ml.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: nameof(PromptInput.IsWorkflow),
                featureColumnName: "Features",
                maximumNumberOfIterations: 100));

        var model = pipeline.Fit(split.TrainSet);

        // Evaluate
        var metrics = ml.BinaryClassification.Evaluate(
            model.Transform(split.TestSet), labelColumnName: nameof(PromptInput.IsWorkflow));

        PrintMetrics(metrics);

        // Save
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        using var stream = File.Create(outputPath);
        ml.Model.Save(model, split.TrainSet.Schema, stream);
    }

    private static void Evaluate(string dataPath)
    {
        var ml = new MLContext(seed: 42);
        var prompts = TrainingDataGenerator.ReadCsv(dataPath);
        var data = ml.Data.LoadFromEnumerable(
            prompts.Select(p => new PromptInput { Prompt = p.Prompt, IsWorkflow = p.IsWorkflow }));
        var split = ml.Data.TrainTestSplit(data, testFraction: 0.2, seed: 42);

        var pipeline = ml.Transforms.Text
            .FeaturizeText("Features", nameof(PromptInput.Prompt))
            .Append(ml.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: nameof(PromptInput.IsWorkflow),
                featureColumnName: "Features",
                maximumNumberOfIterations: 100));

        var model = pipeline.Fit(split.TrainSet);
        var metrics = ml.BinaryClassification.Evaluate(
            model.Transform(split.TestSet), labelColumnName: nameof(PromptInput.IsWorkflow));

        PrintMetrics(metrics);
    }

    private static void PrintMetrics(CalibratedBinaryClassificationMetrics metrics)
    {
        AnsiConsole.MarkupLine("\n[bold]Test set results:[/]");
        AnsiConsole.MarkupLine($"  Accuracy:  [cyan]{metrics.Accuracy:P2}[/]");
        AnsiConsole.MarkupLine($"  AUC:       [cyan]{metrics.AreaUnderRocCurve:P2}[/]");
        AnsiConsole.MarkupLine($"  F1 Score:  [cyan]{metrics.F1Score:P2}[/]");
        AnsiConsole.MarkupLine($"  Precision: [cyan]{metrics.PositivePrecision:P2}[/]");
        AnsiConsole.MarkupLine($"  Recall:    [cyan]{metrics.PositiveRecall:P2}[/]");
    }

    private static void RunBenchmark()
    {
        AnsiConsole.MarkupLine("\n[bold yellow]A/B Benchmark: ML.NET vs TF-IDF[/]\n");

        var prompts = TrainingDataGenerator.Generate(augmentPerSeed: 5);
        AnsiConsole.MarkupLine($"[dim]Dataset: {prompts.Count} prompts[/]");

        var rng = new Random(42);
        var shuffled = prompts.OrderBy(_ => rng.Next()).ToList();
        var evalSet = shuffled.Take((int)(prompts.Count * 0.8)).ToList();
        var trainSet = shuffled.Skip((int)(prompts.Count * 0.8)).ToList();

        // ── TF-IDF ───────────────────────────────────────────────────────
        var tfidf = new TfIdfIntentClassifier();
        var sw = Stopwatch.StartNew();
        var tfidfResults = evalSet
            .Select(p => (p, r: tfidf.Classify(p.Prompt)))
            .ToList();
        sw.Stop();
        var tfidfMs = sw.Elapsed.TotalMilliseconds / evalSet.Count;
        var tfidfCorrect = tfidfResults.Count(x => x.r.IsWorkflow == x.p.IsWorkflow);
        var tfidfAccuracy = (double)tfidfCorrect / evalSet.Count;

        // ── ML.NET ───────────────────────────────────────────────────────
        var ml = new MLContext(seed: 42);
        var trainData = ml.Data.LoadFromEnumerable(
            trainSet.Select(p => new PromptInput { Prompt = p.Prompt, IsWorkflow = p.IsWorkflow }));

        var pipeline = ml.Transforms.Text
            .FeaturizeText("Features", nameof(PromptInput.Prompt))
            .Append(ml.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: nameof(PromptInput.IsWorkflow),
                featureColumnName: "Features",
                maximumNumberOfIterations: 50));

        var model = pipeline.Fit(trainData);
        var engine = ml.Model.CreatePredictionEngine<PromptInput, PromptPrediction>(model);

        sw.Restart();
        var mlResults = evalSet
            .Select(p =>
            {
                var pred = engine.Predict(new PromptInput { Prompt = p.Prompt });
                return (p, IsWorkflow: pred.PredictedLabel, Probability: pred.Probability);
            })
            .ToList();
        sw.Stop();
        var mlMs = sw.Elapsed.TotalMilliseconds / evalSet.Count;
        var mlCorrect = mlResults.Count(x => x.IsWorkflow == x.p.IsWorkflow);
        var mlAccuracy = (double)mlCorrect / evalSet.Count;

        // ── Table ───────────────────────────────────────────────────────
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Benchmark Results[/]")
            .AddColumn("Metric")
            .AddColumn("TF-IDF")
            .AddColumn("ML.NET SdcaLR");
        table.AddRow("Accuracy", $"{tfidfAccuracy:P2}", $"{mlAccuracy:P2}");
        table.AddRow("Avg latency", $"{tfidfMs:F4}ms", $"{mlMs:F4}ms");
        table.AddRow("Correct / Total", $"{tfidfCorrect}/{evalSet.Count}", $"{mlCorrect}/{evalSet.Count}");
        AnsiConsole.Write(table);

        // ── Disagreements ─────────────────────────────────────────────
        var disagreements = tfidfResults
            .Zip(mlResults, (t, m) => (Tfidf: t, Ml: m))
            .Where(x => x.Tfidf.r.IsWorkflow != x.Ml.IsWorkflow)
            .Take(8)
            .ToList();

        if (disagreements.Count > 0)
        {
            AnsiConsole.MarkupLine($"\n[bold]Sample disagreements ({disagreements.Count} total):[/]");
            foreach (var d in disagreements)
            {
                var truncated = d.Tfidf.p.Prompt.Length > 65
                    ? d.Tfidf.p.Prompt[..65] + "…"
                    : d.Tfidf.p.Prompt;
                AnsiConsole.MarkupLine($"  [dim]'{Markup.Escape(truncated)}'[/]");
                AnsiConsole.MarkupLine(
                    $"    TF-IDF: {d.Tfidf.r.IsWorkflow} (conf={d.Tfidf.r.Confidence:F2})  " +
                    $"ML.NET: {d.Ml.IsWorkflow} (p={d.Ml.Probability:F2})  " +
                    $"[dim]→ {d.Tfidf.p.IsWorkflow}[/]");
            }
        }

        // ── Winner ─────────────────────────────────────────────────────
        AnsiConsole.WriteLine();
        var diff = mlAccuracy - tfidfAccuracy;
        if (Math.Abs(diff) < 0.01)
            AnsiConsole.MarkupLine("[yellow]Tied — ML.NET may improve with more real training data.[/]");
        else if (diff > 0)
            AnsiConsole.MarkupLine($"[green]ML.NET wins by {diff:P2}.[/]");
        else
            AnsiConsole.MarkupLine($"[yellow]TF-IDF wins by {-diff:P2} — add more diverse real examples to training data.[/]");
    }
}
