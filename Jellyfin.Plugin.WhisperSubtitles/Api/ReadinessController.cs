using System;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.WhisperSubtitles.Api;

/// <summary>
/// The one path this plugin answers on the server. The configuration page asks it
/// whether the backend an operator has chosen is ready, about the settings as they
/// stand on the page and before a save.
/// </summary>
/// <remarks>
/// A path is a claim on a server this plugin shares with every other installed
/// plugin, and this is the first one this plugin makes. It is written in
/// <c>interoperability/claims/</c>, this file is named in <c>RouteClaimsTests</c>
/// as the one source allowed to claim a path, and the scan over a booted server
/// reads the server's route document to see that it answers here. A second
/// controller is a second claim and goes through all three.
///
/// A POST and not a GET, because the question carries the settings it is about
/// and one of them is a key. Only an elevated session may ask, which is the policy
/// the server itself puts on reading and writing a plugin's configuration: the
/// answer names a path an operator typed, and the local probe reads whether a
/// file is at it, which is not a question every signed-in user gets to ask about
/// the server's disk.
///
/// The server builds this out of its own container, the way it builds the
/// scheduled task, so the three seams arrive from what
/// <see cref="PluginServiceRegistrator"/> registered and nothing is constructed
/// here. Nothing here logs, because nothing in this plugin does yet. The answer
/// carries no key on any path, which the probes hold for every sentence they
/// produce.
/// </remarks>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route(PluginRoute.Prefix)]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ReadinessController : ControllerBase
{
    /// <summary>
    /// The segment every path this plugin answers sits under, which is
    /// <see cref="PluginRoute.Prefix"/> and is kept here for the page and the
    /// tests that spell the path from this type.
    /// </summary>
    public const string Prefix = PluginRoute.Prefix;

    /// <summary>
    /// The segment the readiness question is asked on, under <see cref="Prefix"/>.
    /// </summary>
    public const string ReadinessSegment = "Readiness";

    /// <summary>
    /// The whole path, spelt the way the server's route document and the claim
    /// record spell it.
    /// </summary>
    public const string ReadinessPath = "/" + Prefix + "/" + ReadinessSegment;

    private readonly IProcessRunner _runner;
    private readonly IFileFacts _files;
    private readonly RemoteHttpHandler _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadinessController"/> class.
    /// </summary>
    /// <param name="runner">The seam every child process is started through.</param>
    /// <param name="files">The seam the local backend looks at a path through.</param>
    /// <param name="httpHandler">The seam the remote backend reaches an endpoint through.</param>
    public ReadinessController(IProcessRunner runner, IFileFacts files, RemoteHttpHandler httpHandler)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _http = httpHandler ?? throw new ArgumentNullException(nameof(httpHandler));
    }

    /// <summary>
    /// Asks whether the backend the posted settings choose can be used right now.
    /// </summary>
    /// <param name="settings">The settings as they stand on the page, in the shape a save writes.</param>
    /// <param name="cancellationToken">Stops the question, which is the operator leaving the page.</param>
    /// <returns>The backend the settings choose, whether it is ready, and what stands in the way when it is not.</returns>
    [HttpPost(ReadinessSegment)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReadinessReport>> AskAsync(
        [FromBody] PluginConfiguration? settings,
        CancellationToken cancellationToken)
    {
        var report = await ReadinessQuestion
            .AskAsync(settings, _runner, _files, _http.Handler, cancellationToken)
            .ConfigureAwait(false);

        return Ok(report);
    }
}
