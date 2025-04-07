public interface IHackerNewsService
{
    Task<List<StoryDto>> GetTopStoriesAsync(int count);
}