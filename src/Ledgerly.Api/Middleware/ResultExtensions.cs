using Microsoft.AspNetCore.Mvc;
using Ledgerly.Shared;

namespace Ledgerly.Api.Middleware;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess) return new OkObjectResult(ApiResponse<T>.Ok(result.Value));
        return ToErrorResult(result.Error);
    }

    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess) return new OkObjectResult(ApiResponse.Ok());
        return ToErrorResult(result.Error);
    }

    public static IActionResult ToErrorResult(this Error error)
    {
        var status = error.Code switch
        {
            "not_found" => StatusCodes.Status404NotFound,
            "unauthorized" => StatusCodes.Status401Unauthorized,
            "forbidden" => StatusCodes.Status403Forbidden,
            "plan_limit" => StatusCodes.Status402PaymentRequired,
            "validation" or "weak_password" or "email_in_use" => StatusCodes.Status400BadRequest,
            "invalid_state" => StatusCodes.Status409Conflict,
            "conflict" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return new ObjectResult(ApiResponse.Fail(error.Code, error.Message)) { StatusCode = status };
    }
}