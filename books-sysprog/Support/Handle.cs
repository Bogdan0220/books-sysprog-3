// Support/Handle.cs
using System.Net;
using System.Text;
using System.Text.Json;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using books_sysprog.Support;

namespace books_sysprog.Support;

public static class Handle
{
    // Glavni handler za svaki HTTP zahtev
    public static void Request(HttpListenerContext ctx, Logger log, BooksClient books, Sentiment sent)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var req = ctx.Request;
        var res = ctx.Response;

        var path = req.Url?.AbsolutePath?.ToLowerInvariant() ?? "/";
        var query = req.Url?.Query ?? "";
        log.Info($"REQ {req.HttpMethod} {path}{query} from {req.RemoteEndPoint}");

        try
        {
            switch (path)
            {
                case "/health":
                    WriteJson(res, new { status = "ok" });
                    log.Info($"RES 200 /health ({sw.ElapsedMilliseconds} ms)");
                    break;

                case "/books":
                    Books(req, res, sw, log, books, sent);
                    break;

                default:
                    WriteJson(res, new { error = "Not found" }, HttpStatusCode.NotFound);
                    log.Warn($"RES 404 {path} ({sw.ElapsedMilliseconds} ms)");
                    break;
            }
        }
        catch (Exception ex)
        {
            log.Error(ex.ToString());
            try
            {
                WriteJson(res, new { error = "Internal server error" }, HttpStatusCode.InternalServerError);
                log.Error($"RES 500 {path}{query} ({sw.ElapsedMilliseconds} ms)");
            }
            catch { }
        }
    }

    // Obrada /books rute (Rx pipeline + sentiment)
    private static void Books(
        HttpListenerRequest req,
        HttpListenerResponse res,
        System.Diagnostics.Stopwatch sw,
        Logger log,
        BooksClient books,
        Sentiment sent)
    {
        var qk = req.QueryString.Get("q");
        if (string.IsNullOrWhiteSpace(qk))
        {
            WriteJson(res, new { error = "Missing query parameter 'q'" }, HttpStatusCode.BadRequest);
            log.Warn($"RES 400 /books (q missing) ({sw.ElapsedMilliseconds} ms)");
            return;
        }

        int max = 5;
        if (int.TryParse(req.QueryString.Get("max"), out var m)) max = Math.Clamp(m, 1, 10);

        // Rx pipeline: FromAsync -> SelectMany -> Where -> paralelizacija (Start + TaskPool) -> Merge -> ToArray
        var done = new ManualResetEventSlim(false);
        var errorSent = false;

        Observable
            .FromAsync(() => books.SearchAsync(qk!, max))
            .SelectMany(list => list)
            .Where(b => !string.IsNullOrWhiteSpace(b.Description))
            .Select(b =>
                Observable.Start(() =>
                {
                    var pred = sent.Predict(b.Description!);
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
            .Merge()
            .ToArray()
            .Subscribe(
                arr =>
                {
                    WriteJson(res, arr);
                    log.Info($"RES 200 /books (count={arr.Length}) ({sw.ElapsedMilliseconds} ms)");
                    done.Set();
                },
                ex =>
                {
                    errorSent = true;
                    log.Error(ex.ToString());
                    WriteJson(res, new { error = "Books or Sentiment processing failed" }, HttpStatusCode.InternalServerError);
                    log.Error($"RES 500 /books ({sw.ElapsedMilliseconds} ms)");
                    done.Set();
                },
                () => { if (!errorSent) done.Set(); }
            );

        // Pošto je Request handler na ThreadPool niti, ovde čekamo dok Rx ne završi
        done.Wait();
    }

    // Slanje JSON odgovora
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
