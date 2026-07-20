using System.Net.Mime;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Multify.Api;

/// <summary>
/// API controller for test notifications.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("Multify")]
[Produces(MediaTypeNames.Application.Json)]
public class TestNotificationController : ControllerBase
{
    private readonly IMultifyTestService _testService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestNotificationController"/> class.
    /// </summary>
    /// <param name="testService">Instance of the <see cref="IMultifyTestService"/>.</param>
    public TestNotificationController(IMultifyTestService testService)
    {
        _testService = testService;
    }

    /// <summary>
    /// Sends a test notification to the specified destination.
    /// </summary>
    /// <param name="request">The test notification request.</param>
    /// <returns>A success or failure response.</returns>
    [HttpPost("TestNotification")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TestNotificationResponse>> SendTestNotification([FromBody] TestNotificationRequest request)
    {
        if (string.IsNullOrEmpty(request.DestinationType))
        {
            return BadRequest(new TestNotificationResponse
            {
                Success = false,
                ErrorMessage = "Destination type is required."
            });
        }

        var result = await _testService.SendTestNotificationAsync(request).ConfigureAwait(false);

        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }
}
