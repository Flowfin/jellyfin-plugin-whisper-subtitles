using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Audio;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.WhisperSubtitles.Api;

/// <summary>
/// The two steps of removing what this plugin generated, as the configuration
/// page reaches them: list what would be removed, and remove what was listed.
/// </summary>
/// <remarks>
/// Two paths, both claims on a server this plugin shares, recorded in
/// <c>interoperability/claims/</c> beside the readiness route and named in
/// <c>RouteClaimsTests</c> as this file's. Only an elevated session may ask,
/// because both answers name paths in the operator's library and the second
/// takes files off it.
///
/// The removal takes paths and does not trust them. It lists the record again,
/// keeps only files the record names that the listing finds unchanged and the
/// posted list names, and hands those to the removal that reads each one once
/// more at the moment of deletion. So a posted path the record does not name is
/// never read, and a file edited since the operator saw the list is kept, which
/// is what #43 asks and <see cref="GeneratedSubtitleListing"/> holds.
///
/// The record, the digest and the removal arrive from the composition root. The
/// record is built there over the data directory the server reports for this
/// plugin, so nothing here reaches the static instance to find it.
/// </remarks>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route(PluginRoute.Prefix)]
[Produces(MediaTypeNames.Application.Json)]
public sealed class GeneratedSubtitlesController : ControllerBase
{
    /// <summary>
    /// The segment the listing is asked on, under <see cref="PluginRoute.Prefix"/>.
    /// </summary>
    public const string ListingSegment = "GeneratedSubtitles";

    /// <summary>
    /// The segment a confirmed removal is posted to, under <see cref="PluginRoute.Prefix"/>.
    /// </summary>
    public const string RemovalSegment = "GeneratedSubtitles/Removal";

    /// <summary>
    /// The listing's whole path, spelt as the server's route document and the claim record spell it.
    /// </summary>
    public const string ListingPath = "/" + PluginRoute.Prefix + "/" + ListingSegment;

    /// <summary>
    /// The removal's whole path, spelt as the server's route document and the claim record spell it.
    /// </summary>
    public const string RemovalPath = "/" + PluginRoute.Prefix + "/" + RemovalSegment;

    private readonly IPublishedSubtitleRecord _record;
    private readonly IFileDigest _digest;
    private readonly IFileRemoval _removal;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedSubtitlesController"/> class.
    /// </summary>
    /// <param name="record">The record of what this plugin published.</param>
    /// <param name="digest">The seam a file's bytes are read through.</param>
    /// <param name="removal">The seam a file is taken off the disk through.</param>
    public GeneratedSubtitlesController(IPublishedSubtitleRecord record, IFileDigest digest, IFileRemoval removal)
    {
        _record = record ?? throw new ArgumentNullException(nameof(record));
        _digest = digest ?? throw new ArgumentNullException(nameof(digest));
        _removal = removal ?? throw new ArgumentNullException(nameof(removal));
    }

    /// <summary>
    /// Lists every file the record names and what is at its path now. Removes nothing.
    /// </summary>
    /// <param name="cancellationToken">Stops the listing, which is the operator leaving the page.</param>
    /// <returns>Every recorded file with its state, and the counts.</returns>
    [HttpGet(ListingSegment)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<GeneratedSubtitleReport>> ListAsync(CancellationToken cancellationToken)
    {
        var findings = await GeneratedSubtitleListing.ListAsync(_record, _digest, cancellationToken).ConfigureAwait(false);

        return Ok(new GeneratedSubtitleReport(findings));
    }

    /// <summary>
    /// Removes the files the operator confirmed, and only those still exactly as listed.
    /// </summary>
    /// <param name="request">The paths the listing offered and the operator confirmed.</param>
    /// <param name="cancellationToken">Stops the removal between files.</param>
    /// <returns>What was removed, what was kept because it changed, and what was already gone.</returns>
    [HttpPost(RemovalSegment)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<GeneratedSubtitleRemoval>> RemoveAsync(
        [FromBody] GeneratedSubtitleRemovalRequest? request,
        CancellationToken cancellationToken)
    {
        var confirmed = new HashSet<string>(request?.Paths ?? [], StringComparer.Ordinal);

        var findings = await GeneratedSubtitleListing.ListAsync(_record, _digest, cancellationToken).ConfigureAwait(false);

        var offered = findings.Removable
            .Where(finding => confirmed.Contains(finding.Entry.Path))
            .ToList();

        var removal = await GeneratedSubtitleListing
            .RemoveAsync(offered, _digest, _removal, cancellationToken)
            .ConfigureAwait(false);

        return Ok(removal);
    }
}
