using BestStories.API.DTOs;
using BestStories.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace BestStories.API.Extensions;

internal static class ResultExtensions
{
    internal static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller)
    {
        if (result.IsSuccess)
            return controller.Ok(result.Value);

        var body = new ErrorResponse(result.Error!, result.ErrorCode.ToString());

        return result.ErrorCode switch
        {
            ErrorCode.Validation => controller.BadRequest(body),
            ErrorCode.ExternalApiTimeout => controller.StatusCode(StatusCodes.Status504GatewayTimeout, body),
            ErrorCode.ExternalApiRateLimited => controller.StatusCode(StatusCodes.Status503ServiceUnavailable, body),
            ErrorCode.ExternalApiUnavailable => controller.StatusCode(StatusCodes.Status503ServiceUnavailable, body),
            _ => controller.StatusCode(StatusCodes.Status502BadGateway, body)
        };
    }
}
