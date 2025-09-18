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
            // ThreadPool.QueueUserWorkItem(_ => Handle(ctx)); // obrada na ThreadPool niti      (ovo smo koristili ranije)
            ThreadPool.QueueUserWorkItem(_ => Handle.Request(ctx, _log, _books, _sent));
        }
    }


}
