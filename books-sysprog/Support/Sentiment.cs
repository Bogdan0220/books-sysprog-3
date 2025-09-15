//support za sentiment analizu koristeci ML.NET
using Microsoft.ML;
using Microsoft.ML.Data;

namespace books_sysprog.Support;

public class Sentiment
{
    private readonly MLContext _ml = new(seed: 1);
    private readonly ITransformer _model;
    private readonly PredictionEngine<Input, Output> _engine;

    public Sentiment()
    {
        // mali, ugradjeni skup primera (toy dataset)
        var samples = new[]
        {
            new Input { Text = "I loved this book, it was amazing and inspiring", Label = true },
            new Input { Text = "Great story, wonderful characters", Label = true },
            new Input { Text = "Absolutely fantastic and engaging", Label = true },
            new Input { Text = "Terrible writing, very boring", Label = false },
            new Input { Text = "I hated this, waste of time", Label = false },
            new Input { Text = "Bad plot and weak ending", Label = false },
            new Input { Text = "Brilliant and thought-provoking", Label = true },
            new Input { Text = "Not good, very disappointing", Label = false },
            new Input { Text = "Enjoyable and well written", Label = true },
            new Input { Text = "Mediocre at best, not recommended", Label = false }
        };

        var data = _ml.Data.LoadFromEnumerable(samples);

        var pipeline = _ml.Transforms.Text.FeaturizeText("Features", nameof(Input.Text))
            .Append(_ml.BinaryClassification.Trainers.SdcaLogisticRegression(labelColumnName: nameof(Input.Label), featureColumnName: "Features"));

        _model = pipeline.Fit(data);
        _engine = _ml.Model.CreatePredictionEngine<Input, Output>(_model);
    }

    public (float Score, string Label) Predict(string text)
    {
        var pred = _engine.Predict(new Input { Text = text });
        var label = pred.PredictedLabel ? "Positive" : "Negative";
        return (pred.Probability, label);
    }

    public class Input
    {
        public string Text { get; set; } = "";
        public bool Label { get; set; }
    }

    public class Output
    {
        [ColumnName("PredictedLabel")]
        public bool PredictedLabel { get; set; }
        public float Probability { get; set; }
        public float Score { get; set; }
    }
}
