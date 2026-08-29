using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The quarantine limit as an operator meets it: a number in a file the server
/// writes, typed on a page, read back on every start.
/// </summary>
/// <remarks>
/// <see cref="AttemptLedgerTests"/> judges what the rule does with a limit it is
/// handed, and this judges how a limit gets to it, which is a different set of
/// failures. A rule that is right about every number and is never asked, a sentinel
/// that collides with a value somebody meant, and a file written before the setting
/// existed that comes back as a zero rather than as nobody having chosen, are none
/// of them visible to a test of the rule.
///
/// The neighbour to read this against is <see cref="ResourceLimitSettingsTests"/>.
/// The journey is the same and one thing on it is not: those two limits resolve
/// against the processors a server reports and this one resolves to a constant, so
/// nothing here states a machine and nothing here should.
/// </remarks>
public class QuarantineSettingTests
{
    /// <summary>
    /// The sentence the page states this setting's standing with, and the whole of
    /// what is machine-read out of it.
    /// </summary>
    private const string NothingReadsItYet = "Nothing reads this number yet";

    /// <summary>
    /// A call on the page that reads or writes a number field, with its arguments.
    /// </summary>
    private static readonly Regex Calls = new(
        @"WhisperSubtitlesConfig\.limit(?:Value|Typed)\((?<arguments>[^;]*?)\);",
        RegexOptions.Singleline,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// Which sentinel each number setting on the page means by nobody having chosen.
    /// </summary>
    /// <remarks>
    /// Listed here rather than derived, because what the page ought to do is exactly
    /// what is being checked and a list read off the page would agree with it
    /// whatever it said. The two resource limits are in it so that a change pointing
    /// one of them at this setting's sentinel is caught by the same leg.
    /// </remarks>
    private static readonly Dictionary<string, string> SettingsByTheirSentinel = new(StringComparer.Ordinal)
    {
        [nameof(PluginConfiguration.ItemsAtOnce)] = "letTheMachineDecide",
        [nameof(PluginConfiguration.ThreadsPerItem)] = "letTheMachineDecide",
        [nameof(PluginConfiguration.FailuresBeforeQuarantine)] = "letThePolicyDecide",
    };

    [Fact]
    public void Nobody_choosing_is_the_policy_deciding_rather_than_a_zero()
    {
        // Zero is the absence of a choice and no run can be in it. An item
        // quarantined after zero counted failures would be an item set aside before
        // it had ever been tried, which is the state a fresh install would be in if
        // the sentinel reached the rule.
        var load = ConfigurationValidation.Of(new PluginConfiguration(), processorCount: 8);

        Assert.Empty(load.Complaints);
        Assert.Equal(RetryPolicy.DefaultFailureLimit, load.InForce.FailuresBeforeQuarantine);
    }

    [Fact]
    public void A_file_written_before_this_setting_existed_reads_as_nobody_choosing()
    {
        // The property the sentinel is chosen for. Every configuration this plugin
        // has already written carries no such element, and what this refuses is those
        // files coming back as a run that sets every failing item aside at once.
        var load = ConfigurationFile.Read(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
            + "<PluginConfiguration>"
            + "<SchemaVersion>1</SchemaVersion>"
            + "<Backend></Backend>"
            + "<TargetLanguage>eng</TargetLanguage>"
            + "<LibraryTargets />"
            + "</PluginConfiguration>");

        Assert.Empty(load.Complaints);
        Assert.Equal(RetryPolicy.DefaultFailureLimit, load.InForce.FailuresBeforeQuarantine);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(50)]
    public void A_number_of_failures_is_the_number_the_run_uses(int failures)
    {
        // The whole point of the setting being a setting, and the high value is here
        // on purpose: there is no ceiling, so fifty has to arrive unchanged rather
        // than meeting a bound nobody wrote down.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { FailuresBeforeQuarantine = failures },
            processorCount: 8);

        Assert.Empty(load.Complaints);
        Assert.Equal(failures, load.InForce.FailuresBeforeQuarantine);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-50)]
    public void A_number_below_one_is_complained_about_by_name(int failures)
    {
        // One configuration wrong in exactly one place, so a rule that fires on the
        // wrong field or a rule that fires on everything is visible rather than
        // hidden behind a complaint somebody else's setting produced.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { FailuresBeforeQuarantine = failures },
            processorCount: 8);

        var complaint = Assert.Single(load.Complaints);

        Assert.Equal(nameof(PluginConfiguration.FailuresBeforeQuarantine), complaint.Field);
    }

    [Fact]
    public void The_complaint_names_the_number_that_was_typed_and_what_runs_instead()
    {
        // A refusal that does not repeat the number leaves an operator comparing a
        // page against their own memory of what they typed, and one that does not say
        // what is in force leaves them assuming it is theirs.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { FailuresBeforeQuarantine = -3 },
            processorCount: 8);

        var complaint = Assert.Single(load.Complaints);

        Assert.Contains("-3", complaint.Problem, StringComparison.Ordinal);

        Assert.Contains(
            RetryPolicy.DefaultFailureLimit.ToString(CultureInfo.InvariantCulture),
            complaint.InForce,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_refused_number_leaves_the_default_in_force_and_not_the_floor()
    {
        // The failure this shape exists against, and the one direction a quarantine
        // limit can fail dangerously. Raising a refused number to the smallest legal
        // one would set every failing item aside after a single attempt, on a server
        // whose operator asked for the opposite, and the run would never say so.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { FailuresBeforeQuarantine = -1 },
            processorCount: 8);

        Assert.Equal(RetryPolicy.DefaultFailureLimit, load.InForce.FailuresBeforeQuarantine);
        Assert.NotEqual(RetryPolicy.SmallestFailureLimit, load.InForce.FailuresBeforeQuarantine);
    }

    [Fact]
    public void The_number_in_force_is_the_number_the_rule_quarantines_on()
    {
        // The two halves joined, because each is green on its own while the setting
        // reaches nothing. A limit an operator typed is carried to the rule, and the
        // item is set aside on that attempt and not before it.
        var inForce = ConfigurationValidation
            .Of(new PluginConfiguration { FailuresBeforeQuarantine = 4 }, processorCount: 8)
            .InForce
            .FailuresBeforeQuarantine;

        var item = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var at = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

        ItemAttempt? record = null;

        for (var attempt = 1; attempt < inForce; attempt++)
        {
            record = RetryPolicy.Record(
                record,
                item,
                TranscriptionFailureReason.BackendFailed,
                at,
                inForce);

            Assert.False(
                record.IsQuarantined,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the item was set aside after {record.Failures} failures while the setting in force allows {inForce}"));
        }

        record = RetryPolicy.Record(
            record,
            item,
            TranscriptionFailureReason.BackendFailed,
            at,
            inForce);

        Assert.True(record.IsQuarantined);
        Assert.Equal(inForce, record.Failures);
    }

    [Fact]
    public void The_setting_survives_the_serializer_the_server_stores_it_with()
    {
        // The server persists this type with XmlSerializer, and a value that does not
        // survive the round trip is a saved setting an operator watches come back
        // empty with nothing in any log.
        var serializer = new XmlSerializer(typeof(PluginConfiguration));

        using var written = new StringWriter(CultureInfo.InvariantCulture);
        serializer.Serialize(written, new PluginConfiguration { FailuresBeforeQuarantine = 7 });

        using var read = new StringReader(written.ToString());

        // CA5369 asks for the XmlReader overload with DTD processing off. The subject
        // here is the call the server makes, on a string this test wrote a line
        // earlier.
#pragma warning disable CA5369
        var restored = Assert.IsType<PluginConfiguration>(serializer.Deserialize(read));
#pragma warning restore CA5369

        Assert.Equal(7, restored.FailuresBeforeQuarantine);
    }

    [Fact]
    public void The_page_reads_and_writes_the_setting()
    {
        // ConfigurationShellTests compares the two name sets and would catch a
        // setting reaching no page at all. This names which, so a setting silently
        // swapped for a neighbouring one on the page is a failure here rather than a
        // set that still matches.
        var setting = nameof(PluginConfiguration.FailuresBeforeQuarantine);
        var page = ConfigurationPageSource.Markup();

        Assert.Matches(new Regex(@"\bconfig\." + setting + @"\b", RegexOptions.None, TimeSpan.FromSeconds(5)), page);
        Assert.Matches(new Regex(@"\bconfig\." + setting + @"\s*=", RegexOptions.None, TimeSpan.FromSeconds(5)), page);
        Assert.Contains("id=\"" + setting + "\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void The_page_offers_the_sentinel_the_code_reads_as_nobody_choosing()
    {
        // The page and the rules have to mean the same thing by zero. A page sending
        // its own idea of unset would save a number the rules refuse, and the operator
        // would meet a complaint about a field they never typed in.
        Assert.Contains(
            "letThePolicyDecide: " + ConfigurationValidation.LetThePolicyDecide.ToString(CultureInfo.InvariantCulture),
            ConfigurationPageSource.Markup(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Every_number_field_is_read_through_the_sentinel_its_own_setting_carries()
    {
        // Both sentinels are zero today and they answer different questions, so the
        // page has to keep them apart rather than reading one through the other. A
        // field taking the neighbouring constant is invisible while the numerals
        // agree and is a saved setting that means something else on the day either
        // default moves.
        //
        // Every call is judged rather than the one this branch added, because the
        // failure is a field pointed at the wrong constant and either field can be
        // the one pointed wrongly. The load side and the save side are both here:
        // a page reading through one sentinel and writing through the other would
        // satisfy a check that looked at either alone.
        var page = ConfigurationPageSource.Markup();

        var calls = Calls.Matches(page);

        Assert.Equal(6, calls.Count);

        foreach (var call in calls.Cast<Match>())
        {
            var arguments = call.Groups["arguments"].Value;
            var setting = SettingsByTheirSentinel.Keys.SingleOrDefault(
                name => arguments.Contains(name, StringComparison.Ordinal));

            Assert.True(
                setting is not null,
                $"a number field on the configuration page is read through no setting this check knows: {arguments}");

            var wanted = SettingsByTheirSentinel[setting!];

            Assert.True(
                arguments.Contains("WhisperSubtitlesConfig." + wanted, StringComparison.Ordinal),
                $"{setting} is read through a sentinel that is not {wanted}, so what nobody choosing means for it is decided by another setting's default: {arguments}");
        }
    }

    [Fact]
    public void The_sentinel_is_a_value_the_rule_would_never_accept()
    {
        // What makes the sentinel safe rather than convenient: no number of failures
        // is zero, so it cannot collide with a value an operator meant. Asked of the
        // rule rather than asserted about the constant.
        Assert.NotNull(RetryPolicy.RefuseAsAFailureLimit(ConfigurationValidation.LetThePolicyDecide));
    }

    [Fact]
    public void The_setting_is_one_a_rule_decides()
    {
        // ConfigurationValidationTests compares the whole set both ways. This names
        // the one, so a change dropping it from the list and its property in one edit
        // is visible as this failing rather than as a set that agrees about less.
        Assert.Contains(
            nameof(PluginConfiguration.FailuresBeforeQuarantine),
            ConfigurationValidation.ValidatedFields);
    }

    [Fact]
    public void A_file_this_release_stands_back_from_still_leaves_a_run_it_could_make()
    {
        // A configuration written by a newer release is not read field by field, so
        // its settings come from a second construction of the defaults. That one has
        // to produce a number the rule can act on too, and it is the one nobody looks
        // at.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { SchemaVersion = ConfigurationValidation.CurrentSchemaVersion + 1 },
            processorCount: 8);

        Assert.Single(load.Complaints);
        Assert.Equal(RetryPolicy.DefaultFailureLimit, load.InForce.FailuresBeforeQuarantine);
    }

    [Fact]
    public void A_file_that_will_not_parse_at_all_leaves_a_run_it_could_make()
    {
        // The third construction of the defaults, reached from ConfigurationFile
        // rather than from the rules, and the one a reader of the rules never meets.
        var load = ConfigurationFile.Read("<PluginConfiguration><SchemaVersion>not a number");

        Assert.NotEmpty(load.Complaints);
        Assert.True(load.InForce.FailuresBeforeQuarantine >= RetryPolicy.SmallestFailureLimit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void The_settings_a_run_reads_refuse_a_limit_that_is_not_a_number_of_failures(int failures)
    {
        // The sentinel lives in the file and must never reach the run, so the type the
        // run reads refuses it at construction rather than trusting every caller to
        // have resolved it. Three constructions of these settings exist and a fourth
        // is one branch away.
        Assert.Throws<ArgumentOutOfRangeException>(() => new SettingsInForce(
            ConfigurationValidation.CurrentSchemaVersion,
            ConfigurationValidation.NoBackendChosen,
            ConfigurationValidation.NoTargetLanguage,
            new Dictionary<Guid, string>(),
            ConcurrencyCap.Default,
            ThreadCount.DefaultFor(8),
            ConfigurationValidation.NoPathNamed,
            ConfigurationValidation.NoPathNamed,
            failures));
    }

    [Fact]
    public void The_page_says_nothing_reads_this_number_exactly_while_nothing_does()
    {
        // A field an operator types into is read as a field that does something, and
        // nothing reads this one: what would apply it is the run this plugin does not
        // perform. So the page says so, and the sentence is read rather than trusted,
        // in both directions.
        //
        // The quiet direction is the sentence surviving the arrival of a reader, which
        // leaves the page denying an effect the setting now has, at exactly the change
        // least likely to remember a page two directories away. The loud direction is
        // the sentence being dropped while nothing reads the number, which credits the
        // field with an effect it does not have.
        var reading = FilesOutsideTheConfigurationThatNameTheSetting();
        var says = ConfigurationPageSource.Markup().Contains(NothingReadsItYet, StringComparison.Ordinal);

        Assert.True(
            !says || reading.Count == 0,
            $"{reading.Count} file(s) outside Configuration/ read the quarantine limit: {string.Join(", ", reading)}. The configuration page still says \"{NothingReadsItYet}\", which denies an effect the setting now has.");

        Assert.True(
            says || reading.Count > 0,
            $"the configuration page no longer says \"{NothingReadsItYet}\" and no file outside Configuration/ names the setting, so the page credits a field with an effect this tree does not give it.");
    }

    /// <summary>
    /// The plugin source files outside the configuration that name the quarantine
    /// limit, by path relative to the project.
    /// </summary>
    /// <remarks>
    /// The same reading <see cref="ResourceLimitSettingsTests"/> makes over its own
    /// two settings, and the same bounds apply: the checkout rather than the
    /// assembly, so a mention in a remark counts; the configuration directory
    /// excluded, because declaring, validating and resolving the setting all name it
    /// by definition; and the plugin project rather than the tree, so a test double
    /// naming it is not the plugin using it.
    ///
    /// The rule that carries the number is deliberately in scope and does not match.
    /// <see cref="RetryPolicy"/> takes a failure limit as a parameter and has never
    /// heard of the setting, which is exactly the state the sentence on the page
    /// describes: a number an operator can set and nothing that fetches it.
    /// </remarks>
    /// <returns>The paths, relative to the plugin project.</returns>
    private static List<string> FilesOutsideTheConfigurationThatNameTheSetting()
    {
        var project = Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.WhisperSubtitles");

        Assert.True(Directory.Exists(project), $"the plugin project was not found at {project}");

        var configuration = Path.Combine(project, "Configuration");
        var sources = Directory.GetFiles(project, "*.cs", SearchOption.AllDirectories);

        Assert.NotEmpty(sources);

        return sources
            .Where(path => !path.StartsWith(configuration, StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path)
                .Contains(nameof(PluginConfiguration.FailuresBeforeQuarantine), StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(project, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
