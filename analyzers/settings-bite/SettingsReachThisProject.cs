namespace AnalyzerSettingsBite;

/// <summary>
/// Three mistakes, chosen so that each one is decided by a different half of the
/// inherited settings, and each is the mistake somebody actually makes rather than
/// one invented to be caught.
/// </summary>
/// <remarks>
/// The type is not sealed, which is CA1852. That rule is off in the analysis mode
/// a project gets by default and on under AllEnabledByDefault, so its presence is
/// what says AnalysisMode arrived here.
///
/// The comment inside the method has no space after its marker, which is SA1005.
/// That rule lives in the StyleCop package, so its presence says the analyzer
/// package references arrived here too.
///
/// The call to Twice is unqualified, which is SA1101, and jellyfin.ruleset turns
/// SA1101 off. Its ABSENCE is what says the ruleset was read: a build that lost
/// CodeAnalysisRuleSet would report it alongside the other two.
/// </remarks>
internal class SettingsReachThisProject
{
    internal int Doubled(int value)
    {
        //no space after the comment marker
        return Twice(value);
    }

    internal int Twice(int value) => value * 2;
}
