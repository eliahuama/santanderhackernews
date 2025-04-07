using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class StoriesController : ControllerBase
{
    private readonly IHackerNewsService _service;

    public StoriesController(IHackerNewsService service)
    {
        _service = service;
    }

    [HttpGet("top")]
    [HttpGet("top-{count}")]
   public async Task<IActionResult> GetTopStories([FromRoute] int count = 10)
    {
        if (count <= 0 || count > 10000) return BadRequest("Count out of range, you can retrieve up to 10000 stories");
        var stories = await _service.GetTopStoriesAsync(count);
        return Ok(stories);
    }
}