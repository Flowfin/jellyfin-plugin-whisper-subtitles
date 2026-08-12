namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

/// <summary>
/// What can be said about a path without opening what is at the end of it.
/// </summary>
/// <remarks>
/// One answer for one path, gathered in one call, because a probe that asked
/// three questions separately would be describing three moments and reporting
/// them as one. A file removed between the second and the third would produce a
/// sentence that was never true of anything.
/// </remarks>
public sealed class FileFacts
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileFacts"/> class.
    /// </summary>
    /// <param name="exists">Whether a file is there.</param>
    /// <param name="sizeInBytes">How large it is, or zero when it is not there.</param>
    /// <param name="isExecutable">Whether it may be executed, or null where nothing was asked.</param>
    public FileFacts(bool exists, long sizeInBytes, bool? isExecutable)
    {
        Exists = exists;
        SizeInBytes = sizeInBytes;
        IsExecutable = isExecutable;
    }

    /// <summary>
    /// Gets a value indicating whether a file is there.
    /// </summary>
    /// <remarks>
    /// A file, and a directory is not one. A path an operator meant to be a tool
    /// and typed as its containing folder is the ordinary version of this mistake,
    /// and reporting it as present would send them looking somewhere else.
    /// </remarks>
    public bool Exists { get; }

    /// <summary>
    /// Gets how large the file is, or zero when there is none.
    /// </summary>
    public long SizeInBytes { get; }

    /// <summary>
    /// Gets a value indicating whether the file may be executed, or null where the
    /// question was not answered.
    /// </summary>
    /// <remarks>
    /// Three states rather than two, and the third is the reason this is nullable.
    /// A permission bit is a property of the platform rather than of the file: it
    /// decides what may run on the systems that carry one, and on the ones that do
    /// not there is nothing to read. Null says the question was not answered, and a
    /// probe that turned that into false would refuse a perfectly good tool on
    /// every machine without the bit.
    /// </remarks>
    public bool? IsExecutable { get; }
}
