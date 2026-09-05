using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

/// <summary>
/// Asks the transcription tool what it is, twice at most, and reports only what
/// it printed.
/// </summary>
/// <remarks>
/// The invocation is the one #324 decided on 2026-09-05. First <c>--version</c>:
/// a tool that exits cleanly and prints a non-empty line has reported its
/// version. Otherwise <c>--help</c>: the first non-empty line is the tool's own
/// description, and it is labelled as that. A tool that prints nothing to either
/// is present and silent. Both run without a model and without audio, and each
/// has a deadline of its own, because the page an operator is waiting in front of
/// is what sits at the other end of this.
///
/// This is the first thing in the plugin that runs the operator's tool on a page
/// load rather than in a run, which is why what it does is small: two flags, one
/// line read out of each, and the rest of the output left unread. What the tool
/// prints is text from a program this repository did not build, so the one line
/// kept is cut to <see cref="LongestLine"/> characters and stripped of control
/// characters before it reaches anybody's screen; `docs/untrusted-input.md`
/// names this as the bound.
///
/// The process is started through the injected runner and nowhere else, and a
/// runner that refuses to start it is the caller's to report: a tool that cannot
/// be started is not a tool that can transcribe.
/// </remarks>
public static class ToolIdentityProbe
{
    /// <summary>
    /// The flag asked first, whose answer is a version when the exit is clean.
    /// </summary>
    public const string VersionFlag = "--version";

    /// <summary>
    /// The flag asked second, whose first line is the tool describing itself.
    /// </summary>
    public const string HelpFlag = "--help";

    /// <summary>
    /// The most of one printed line that is kept.
    /// </summary>
    /// <remarks>
    /// Two hundred characters, which is longer than any version line and shorter
    /// than a screen. A tool that prints a paragraph to <c>--version</c> is shown
    /// the start of it and the page is not asked to render the rest.
    /// </remarks>
    public const int LongestLine = 200;

    /// <summary>
    /// Asks the tool what it is.
    /// </summary>
    /// <param name="runner">The seam every child process is started through.</param>
    /// <param name="executablePath">The tool, as the operator named it.</param>
    /// <param name="answerTimeout">How long each flag may take before the tool is stopped and read as silent for it.</param>
    /// <param name="cancellationToken">Stops the question, which is the operator leaving the page.</param>
    /// <returns>What the tool said, labelled as what it was.</returns>
    /// <exception cref="Exception">Whatever the runner throws when the tool cannot be started; the caller turns that into a reason.</exception>
    public static async Task<ToolIdentity> AskAsync(
        IProcessRunner runner,
        string executablePath,
        TimeSpan answerTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(answerTimeout, TimeSpan.Zero);

        var version = await FirstLineAsync(runner, executablePath, VersionFlag, answerTimeout, cancellationToken).ConfigureAwait(false);

        if (version.ExitCode == 0 && version.Line is not null)
        {
            return ToolIdentity.Version(version.Line);
        }

        var help = await FirstLineAsync(runner, executablePath, HelpFlag, answerTimeout, cancellationToken).ConfigureAwait(false);

        return help.Line is null ? ToolIdentity.Silent() : ToolIdentity.Description(help.Line);
    }

    /// <summary>
    /// The first non-empty line the tool prints to one flag, and how it exited.
    /// The output is read to its end so the exit code is the tool's own, under a
    /// deadline that stops a tool which never ends and reads it as silent.
    /// </summary>
    private static async Task<(string? Line, int? ExitCode)> FirstLineAsync(
        IProcessRunner runner,
        string executablePath,
        string flag,
        TimeSpan answerTimeout,
        CancellationToken cancellationToken)
    {
        using var process = runner.Start(new ProcessInvocation(executablePath, [flag]));

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(answerTimeout);

        string? first = null;

        try
        {
            await foreach (var line in process.StandardOutputLines.WithCancellation(deadline.Token).ConfigureAwait(false))
            {
                if (first is null && !string.IsNullOrWhiteSpace(line))
                {
                    first = Kept(line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Killed here and not through a registration on the deadline, because
            // a registration can be disposed before the token has run it: the
            // stream's own cancellation callback resumes this method inline, the
            // method returns, and a registration made here is unregistered on the
            // way out with the tool still running. A tool the probe stopped
            // reading is stopped, whichever of the two tokens did it.
            process.Kill();

            // The caller's token wins the tie: somebody who left the page has
            // learned nothing about their tool. The deadline is the tool being
            // silent for this flag, whatever it printed before it stalled.
            cancellationToken.ThrowIfCancellationRequested();

            return (null, null);
        }

        // A stream that ends on the token rather than throwing on it arrives here
        // with the same two states, and they are told apart the same way.
        if (cancellationToken.IsCancellationRequested || deadline.IsCancellationRequested)
        {
            process.Kill();
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (deadline.IsCancellationRequested)
        {
            return (null, null);
        }

        var exitCode = await process.WaitForExitAsync().ConfigureAwait(false);

        return (first, exitCode);
    }

    /// <summary>
    /// The line as it is shown: trimmed, without control characters, and no
    /// longer than <see cref="LongestLine"/>.
    /// </summary>
    private static string Kept(string line)
    {
        var printable = new string(line.Where(character => !char.IsControl(character)).ToArray()).Trim();

        return printable.Length <= LongestLine ? printable : printable[..LongestLine];
    }
}
