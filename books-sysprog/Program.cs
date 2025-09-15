using System.Net;
using System.Text;
using System.Text.Json;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using books_sysprog.Support;

namespace books_sysprog;

public class Program
{
    private static readonly Logger _log = new();
    private static readonly BooksClient _books = new();
    private static readonly Sentiment _sent = new();

    public static void Main(string[] args)
    {
        var listener = new HttpListener();
        var prefix = "http://localhost:5057/"; 
        listener.Prefixes.Add(prefix);
        listener.Start();
        _log.Info($"Server start → {prefix}");
        _log.Info("CTRL+C za kraj.");

        while (true)
        {
            var ctx = listener.GetContext();           // blokira dok ne stigne zahtev
            ThreadPool.QueueUserWorkItem(_ => Handle(ctx)); // obrada na ThreadPool niti (može i async, ali ovo je minimalno)
        }
    }

    private static void Handle(HttpListenerContext ctx)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var req = ctx.Request;
        var res = ctx.Response;

        var path = req.Url?.AbsolutePath?.ToLowerInvariant() ?? "/";
        var query = req.Url?.Query ?? "";
        _log.Info($"REQ {req.HttpMethod} {path}{query} from {req.RemoteEndPoint}");

        try
        {
            switch (path)
            {
                case "/health":
                    WriteJson(res, new { status = "ok" });
                    _log.Info($"RES 200 /health ({sw.ElapsedMilliseconds} ms)");
                    break;

                case "/books":
                    HandleBooks(req, res, sw);
                    break;

                default:
                    WriteJson(res, new { error = "Not found" }, HttpStatusCode.NotFound);
                    _log.Warn($"RES 404 {path} ({sw.ElapsedMilliseconds} ms)");
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex.ToString());
            try
            {
                WriteJson(res, new { error = "Internal server error" }, HttpStatusCode.InternalServerError);
                _log.Error($"RES 500 {path}{query} ({sw.ElapsedMilliseconds} ms)");
            }
            catch { }
        }
    }

    private static void HandleBooks(HttpListenerRequest req, HttpListenerResponse res, System.Diagnostics.Stopwatch sw)
    {
        var qk = req.QueryString.Get("q");
        if (string.IsNullOrWhiteSpace(qk))
        {
            WriteJson(res, new { error = "Missing query parameter 'q'" }, HttpStatusCode.BadRequest);
            _log.Warn($"RES 400 /books (q missing) ({sw.ElapsedMilliseconds} ms)");
            return;
        }

        int max = 5;
        if (int.TryParse(req.QueryString.Get("max"), out var m)) max = Math.Clamp(m, 1, 10);

        // Rx pipeline:
        // 1) dovuci rezultate sa Google Books (async → FromAsync)
        // 2) razvuci listu u stream knjiga (SelectMany)
        // 3) filtriraj bez opisa
        // 4) sentiment na TaskPoolScheduler (paralelno)
        // 5) skupi nazad u niz i pošalji
        var done = new ManualResetEventSlim(false);
        var errorSent = false;

        Observable
            .FromAsync(() => _books.SearchAsync(qk!, max))
            .SelectMany(list => list) // kroz svaku knjigu
            .Where(b => !string.IsNullOrWhiteSpace(b.Description))
            .Select(b =>
                Observable.Start(() =>
                {
                    var pred = _sent.Predict(b.Description!);
                    return new BookSentimentDto
                    {
                        Title = b.Title ?? "(no title)",
                        Authors = b.Authors?.Length > 0 ? string.Join(", ", b.Authors) : "(unknown)",
                        Description = b.Description!,
                        SentimentScore = pred.Score,
                        SentimentLabel = pred.Label
                    };
                }, TaskPoolScheduler.Default)
            )
            .Merge() // spoji paralelne računice
            .ToArray()
            .Subscribe(
                arr =>
                {
                    WriteJson(res, arr);
                    _log.Info($"RES 200 /books (count={arr.Length}) ({sw.ElapsedMilliseconds} ms)");
                    done.Set();
                },
                ex =>
                {
                    errorSent = true;
                    _log.Error(ex.ToString());
                    WriteJson(res, new { error = "Books or Sentiment processing failed" }, HttpStatusCode.InternalServerError);
                    _log.Error($"RES 500 /books ({sw.ElapsedMilliseconds} ms)");
                    done.Set();
                },
                () => { if (!errorSent) done.Set(); }
            );

        // sačekaj dok Rx ne završi (jer smo u ThreadPool handleru)
        done.Wait();
    }

    private static void WriteJson(HttpListenerResponse res, object obj, HttpStatusCode code = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        res.StatusCode = (int)code;
        res.ContentType = "application/json";
        res.ContentEncoding = Encoding.UTF8;
        res.ContentLength64 = bytes.Length;
        res.OutputStream.Write(bytes, 0, bytes.Length);
        res.Close();
    }
}

// DTO za izlaz (držaćemo ga u Program.cs zbog minimalizma)
public class BookSentimentDto
{
    public string Title { get; set; } = "";
    public string Authors { get; set; } = "";
    public string Description { get; set; } = "";
    public float SentimentScore { get; set; }
    public string SentimentLabel { get; set; } = "";
}
