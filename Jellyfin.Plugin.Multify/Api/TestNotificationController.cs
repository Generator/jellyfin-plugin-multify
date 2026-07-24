using System.Net.Mime;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Services;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Api;

/// <summary>
/// API controller for test notifications.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("Multify")]
[Produces(MediaTypeNames.Application.Json)]
public class TestNotificationController : ControllerBase
{
    private readonly IMultifyTestService _testService;
    private readonly ILogger<TestNotificationController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestNotificationController"/> class.
    /// </summary>
    /// <param name="testService">Instance of the <see cref="IMultifyTestService"/>.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TestNotificationController}"/> interface.</param>
    public TestNotificationController(IMultifyTestService testService, ILogger<TestNotificationController> logger)
    {
        _testService = testService;
        _logger = logger;
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
            _logger.LogWarning("Test notification request missing destination type");
            return BadRequest(new TestNotificationResponse
            {
                Success = false,
                ErrorMessage = "Destination type is required."
            });
        }

        _logger.LogInformation("Test notification requested for {DestinationType}", request.DestinationType);
        var result = await _testService.SendTestNotificationAsync(request).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogInformation("Test notification sent successfully to {DestinationType}", request.DestinationType);
            return Ok(result);
        }

        _logger.LogWarning("Test notification failed for {DestinationType}: {ErrorMessage}", request.DestinationType, result.ErrorMessage);
        return BadRequest(result);
    }
}
