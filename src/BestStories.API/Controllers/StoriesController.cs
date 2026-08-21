using BestStories.API.AppServices;
using BestStories.API.DTOs;
using BestStories.API.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace BestStories.API.Controllers;

[ApiController]
[Route("stories")]
[Produces("application/json")]
public sealed class StoriesController(
    IStoriesAppService storiesAppService,
    ILogger<StoriesController> logger) : ControllerBase
{
    /// <summary>
    /// Returns the best <paramref name="n"/> stories from Hacker News, ordered by score descending.
    /// </summary>
    /// <param name="n">Number of stories to return (must be between 1 and 500).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of the top n stories ordered by score.</returns>
    [HttpGet("{n:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<StoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> GetBestStories([FromRoute] int n, CancellationToken cancellationToken)
    {
        logger.LogDebug("Request received for top {Count} stories", n);

        var result = await storiesAppService.GetBestStoriesAsync(n, cancellationToken);

        return result.ToActionResult(this);
    }
}
