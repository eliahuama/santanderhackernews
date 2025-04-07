using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

public class HackerNewsService : IHackerNewsService
{
   private readonly IHttpClientFactory _httpClientFactory;
   private readonly IMemoryCache _cache;
   private readonly ILogger<HackerNewsService> _logger;

   private const string BaseUrl = "https://hacker-news.firebaseio.com/v0/";
   private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

   public HackerNewsService(
       IHttpClientFactory httpClientFactory,
       IMemoryCache cache,
       ILogger<HackerNewsService> logger)
   {
      _httpClientFactory = httpClientFactory;
      _cache = cache;
      _logger = logger;
   }

   public async Task<List<StoryDto>> GetTopStoriesAsync(int count)
   {
      var client = _httpClientFactory.CreateClient();

      var storyIds = await GetBestStoryIdsAsync(client);
      if (storyIds == null || !storyIds.Any()) return new List<StoryDto>();

      var throttler = new SemaphoreSlim(10); // max 10 concurrent fetches
      var tasks = storyIds.Take(500).Select(async id =>
      {
         await throttler.WaitAsync();
         try
         {
            return await GetStoryAsync(client, id);
         }
         finally
         {
            throttler.Release();
         }
      });

      var stories = await Task.WhenAll(tasks);
      return stories
          .OfType<StoryDto>()
          .OrderByDescending(s => s.Score)
          .Take(count)
          .ToList();
   }

   private async Task<List<int>> GetBestStoryIdsAsync(HttpClient client)
   {
      return (await _cache.GetOrCreateAsync("beststoryids", async entry =>
      {
         entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);

         try
         {
            var json = await client.GetStringAsync($"{BaseUrl}beststories.json");

            if (string.IsNullOrWhiteSpace(json))
            {
               _logger.LogWarning("Received empty beststories.json response.");
               return null; ;
            }

            try
            {
               var ids = JsonSerializer.Deserialize<List<int>>(json, _jsonOptions);
               return ids;
            }
            catch (JsonException ex)
            {
               _logger.LogError(ex, "Failed to deserialize best story IDs.");
               return new List<int>();
            }
         }
         catch (Exception ex)
         {
            _logger.LogError(ex, "Failed to fetch best story IDs from Hacker News.");
            return null;
         }
      })) ?? new List<int>();
   }

   private async Task<StoryDto?> GetStoryAsync(HttpClient client, int id)
   {
      return await _cache.GetOrCreateAsync($"story_{id}", async entry =>
      {
         entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
         try
         {
            var json = await client.GetStringAsync($"{BaseUrl}item/{id}.json");
            var raw = JsonSerializer.Deserialize<HackerNewsStory>(json, _jsonOptions);

            if (raw == null || string.IsNullOrEmpty(raw.Title)) return null;

            return new StoryDto
            {
               Title = raw.Title,
               Uri = raw.Url,
               PostedBy = raw.By,
               Time = DateTimeOffset.FromUnixTimeSeconds(raw.Time.GetValueOrDefault()).UtcDateTime,
               Score = raw.Score,
               CommentCount = raw.Descendants
            };
         }
         catch (Exception ex)
         {
            _logger.LogWarning(ex, $"Failed to fetch story {id}: {ex}");
            return null;
         }
      });
   }
}