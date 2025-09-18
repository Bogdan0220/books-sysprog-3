// support za sentiment analizu koristeci ML.NET
using System.Threading;      
using Microsoft.ML;
using Microsoft.ML.Data;

namespace books_sysprog.Support;

public class Sentiment
{
    private readonly MLContext _ml = new(seed: 1);
    private readonly ITransformer _model;

    // Po JEDAN PredictionEngine po niti (thread-safe)
    private readonly ThreadLocal<PredictionEngine<Input, Output>> _engines;

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

        var pipeline =
            _ml.Transforms.Text.FeaturizeText("Features", nameof(Input.Text))
               .Append(_ml.BinaryClassification.Trainers.SdcaLogisticRegression(
                   labelColumnName: nameof(Input.Label),
                   featureColumnName: "Features"));

        _model = pipeline.Fit(data);

        // ← ISPRAVNO: factory koji pravi PredictionEngine za SVAKU NIT posebno
        _engines = new ThreadLocal<PredictionEngine<Input, Output>>(
            () => _ml.Model.CreatePredictionEngine<Input, Output>(_model),
            trackAllValues: false
        );
    }

    public (float Score, string Label) Predict(string text)
    {
        // Uzmi engine za TEKUĆU nit
        var engine = _engines.Value!;
        var pred = engine.Predict(new Input { Text = text ?? "" });
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
