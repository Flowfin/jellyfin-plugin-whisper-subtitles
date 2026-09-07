using System;
using System.Collections.Generic;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Jellyfin.Plugin.WhisperSubtitles.Selection;

namespace Jellyfin.Plugin.WhisperSubtitles.Library;

/// <summary>
/// The seam between this plugin and the server's library.
/// </summary>
/// <remarks>
/// Every part of a run takes its input as a value.
/// <see cref="ItemSelection"/> is a pure function over
/// <see cref="ItemDescription"/>s and publication decides from an
/// <see cref="ItemLocation"/>, and until this existed nothing turned the server's
/// library into either of them. That is what this is for and it is the whole of
/// it.
///
/// TWO QUESTIONS AND NO MORE, and the narrowness is the point rather than an
/// economy. A seam wide enough to answer whatever a caller needs next is a server
/// interface with a different name on it, and every member added here is one more
/// thing a double has to be faithful about. What a run needs is the population it
/// may consider and, for one item it chose, where that item's files are.
///
/// It hands back the flat descriptions rather than server types, for the reason
/// those two types were written flat: a test cannot fabricate a library out of
/// types that exist only inside a running server, so a seam returning them would
/// leave selection as untestable as it was before the seam.
///
/// SYNCHRONOUS, because the thing behind it is. The server's library manager
/// answers both of these from its own store without a call that waits on anything
/// outside the machine, and an asynchronous signature over a synchronous
/// implementation is a claim about waiting that nothing here makes. Where that
/// stops being true it is a change to this interface argued in its own issue, not
/// a wrapper somebody adds quietly.
/// </remarks>
public interface ILibrarySource
{
    /// <summary>
    /// The items a run may consider.
    /// </summary>
    /// <returns>One description per item, in no particular order.</returns>
    /// <remarks>
    /// The population and never the selection. Which of these a run actually takes
    /// is <see cref="ItemSelection"/>'s decision, made from the bounds an operator
    /// set, and an implementation that filtered here would be a second place those
    /// bounds live.
    ///
    /// The dotted name of that method is deliberately not written here.
    /// <c>SECURITY.md</c> pastes a search for it to show that selection sits behind
    /// no route a server takes, and a mention in a comment would put a line into
    /// that paste which is not a call.
    ///
    /// The order is not promised. Selection orders what it returns, so an
    /// implementation is free to hand these back in whatever order its store
    /// produces them.
    /// </remarks>
    IReadOnlyList<ItemDescription> ItemsUnderConsideration();

    /// <summary>
    /// Where one item's files are.
    /// </summary>
    /// <param name="itemId">The item to locate.</param>
    /// <returns>The location, or null where the library has no usable answer.</returns>
    /// <remarks>
    /// Null rather than an exception, and it covers two cases that look the same
    /// from here: an item the library no longer holds, and one it holds with no
    /// path on it. A run that selected an item and then found it gone between the
    /// selection and the write is an ordinary thing on a library somebody is
    /// editing, and the caller reports it rather than failing the run.
    /// </remarks>
    ItemLocation? LocationOf(Guid itemId);
}
