//klijentski deo + DTO-ovi zajedno
using System.Net.Http;
using System.Text.Json;

namespace books_sysprog.Support;

public class BooksClient
{
    private readonly HttpClient _http = new();

    public async Task<List<BookRaw>> SearchAsync(string query, int max)
    {
        // Google Books API – basic search
        // Ne traži API key za osnovno.
        var url = $"https://www.googleapis.com/books/v1/volumes?q={Uri.EscapeDataString(query)}&maxResults={max}";
        var resp = await _http.GetAsync(url);
        var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Google Books error {(int)resp.StatusCode}: {text}");

        var root = JsonSerializer.Deserialize<BooksRoot>(text, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var list = new List<BookRaw>();
        if (root?.Items == null) return list;

        foreach (var item in root.Items)
        {
            var vi = item.VolumeInfo;
            if (vi == null) continue;

            list.Add(new BookRaw
            {
                Title = vi.Title,
                Authors = vi.Authors,
                Description = vi.Description
            });
        }
        return list;
    }
}

// DTO-ovi samo za deserializaciju ulaza
public class BooksRoot
{
    public List<BookItem>? Items { get; set; }
}

public class BookItem
{
    public VolumeInfo? VolumeInfo { get; set; }
}

public class VolumeInfo
{
    public string? Title { get; set; }
    public string[]? Authors { get; set; }
    public string? Description { get; set; }
}

public class BookRaw
{
    public string? Title { get; set; }
    public string[]? Authors { get; set; }
    public string? Description { get; set; }
}

// DTO za izlaz 
public class BookSentimentDto
{
    public string Title { get; set; } = "";
    public string Authors { get; set; } = "";
    public string Description { get; set; } = "";
    public float SentimentScore { get; set; }
    public string SentimentLabel { get; set; } = "";
}