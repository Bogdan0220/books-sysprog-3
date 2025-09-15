namespace books_sysprog.Support;

public class Logger
{
    private static readonly object _lock = new();
    public void Info(string msg) => Write("INFO", msg);
    public void Warn(string msg) => Write("WARN", msg);
    public void Error(string msg) => Write("ERROR", msg);

    private void Write(string level, string msg)
    {
        lock (_lock)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {level} {msg}");
        }
    }
}
