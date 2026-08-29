using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The two resource limits as an operator meets them: a number in a file the
/// server writes, typed on a page, read back on every start.
/// </summary>
/// <remarks>
/// <see cref="ConcurrencyCapTests"/> and <see cref="ThreadCountTests"/> judge the
/// arithmetic and this judges the journey, which is a different set of failures. A
/// rule that is right about every number and is never asked, a sentinel that
/// collides with a value somebody meant, and a file written before a setting
/// existed that comes back as a zero rather than as a machine deciding, are none of
/// them visible to a test of the rule.
///
/// The machine is stated rather than read, everywhere below. A test computing its
/// expected value from <see cref="Environment.ProcessorCount"/> the same way the
/// code does would be the code agreeing with itself, and it would assert a
/// different thing on the machine that ran it.
/// </remarks>
public class ResourceLimitSettingsTests
{
    /// <summary>
    /// The sentence the page states its own standing with, and the whole of what is
    /// machine-read out of it.
    /// </summary>
    /// <remarks>
    /// IT SAID "Neither number reaches a transcription yet" UNTIL THE DRY RUN
    /// LANDED. The disclosure the page owes moved when the first reader beyond the
    /// configuration arrived: the numbers are read now, by something that says what
    /// a run would cost, and a page still saying nothing reads them would be the
    /// loud direction below arriving for real. What has not moved is that neither
    /// number reaches a transcription, because nothing performs a run, and the page
    /// still says that in the same paragraph.
    /// </remarks>
    private const string ReadByTheDryRun = "Both numbers are read by the dry run";

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(64)]
    public void Nobody_choosing_is_the_machine_deciding_rather_than_a_zero(int processors)
    {
        // Zero is the absence of a choice and no run can be in it. What it resolves
        // to is the documented default for each limit, and for the thread count that
        // is a reading of the machine rather than a constant, so it is asked of
        // several machines rather than of one.
        var load = ConfigurationValidation.Of(new PluginConfiguration(), processors);

        Assert.Empty(load.Complaints);
        Assert.Equal(ConcurrencyCap.Default, load.InForce.ItemsAtOnce);
        Assert.Equal(ThreadCount.DefaultFor(processors), load.InForce.ThreadsPerItem);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(8, 4)]
    [InlineData(9, 4)]
    [InlineData(64, 32)]
    public void The_thread_default_leaves_the_machine_something(int processors, int expected)
    {
        // The relation the default exists for, restated at the end of the journey
        // rather than only at the arithmetic: whatever a run takes, the server keeps
        // the rest. Values rather than a formula, for the reason above.
        var load = ConfigurationValidation.Of(new PluginConfiguration(), processors);

        Assert.Equal(expected, load.InForce.ThreadsPerItem);
        Assert.True(
            load.InForce.ThreadsPerItem < processors,
            string.Create(
                CultureInfo.InvariantCulture,
                $"a default of {load.InForce.ThreadsPerItem} on {processors} processors takes the machine an operator did not offer"));
    }

    [Fact]
    public void A_file_written_before_these_settings_existed_reads_as_nobody_choosing()
    {
        // The property the sentinel is chosen for. Every configuration this plugin
        // has already written carries neither element, and what this refuses is those
        // files coming back as a run asking for zero items on zero threads.
        var load = ConfigurationFile.Read(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
            + "<PluginConfiguration>"
            + "<SchemaVersion>1</SchemaVersion>"
            + "<Backend></Backend>"
            + "<TargetLanguage>eng</TargetLanguage>"
            + "<LibraryTargets />"
            + "</PluginConfiguration>");

        Assert.Empty(load.Complaints);
        Assert.True(load.InForce.ItemsAtOnce >= 1);
        Assert.True(load.InForce.ThreadsPerItem >= 1);
    }

    [Theory]
    [InlineData(4, 3, 2)]
    [InlineData(8, 8, 8)]
    [InlineData(1, 1, 1)]
    public void A_number_this_machine_can_carry_is_the_number_the_run_uses(
        int processors,
        int items,
        int threads)
    {
        // The whole point of the setting being a setting. A value inside the range
        // reaches the run unchanged, so an operator who typed three sees three.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { ItemsAtOnce = items, ThreadsPerItem = threads },
            processors);

        Assert.Empty(load.Complaints);
        Assert.Equal(items, load.InForce.ItemsAtOnce);
        Assert.Equal(threads, load.InForce.ThreadsPerItem);
    }

    [Theory]
    [InlineData(nameof(PluginConfiguration.ItemsAtOnce), 9, 0)]
    [InlineData(nameof(PluginConfiguration.ItemsAtOnce), -1, 0)]
    [InlineData(nameof(PluginConfiguration.ThreadsPerItem), 0, 9)]
    [InlineData(nameof(PluginConfiguration.ThreadsPerItem), 0, -1)]
    public void A_number_outside_the_range_is_complained_about_by_name(
        string field,
        int items,
        int threads)
    {
        // One configuration per limit, each wrong in exactly one place and in each
        // direction, so a rule that fires on the wrong field or a rule that fires on
        // everything is visible. Eight processors, so nine is above the ceiling and
        // nothing else here is outside anything.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { ItemsAtOnce = items, ThreadsPerItem = threads },
            processorCount: 8);

        var complaint = Assert.Single(load.Complaints);

        Assert.Equal(field, complaint.Field);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(64)]
    public void A_number_above_the_ceiling_leaves_the_default_in_force_and_not_the_ceiling(int asked)
    {
        // The failure this whole shape exists against. Clamping would hand the run
        // eight and hand the operator nothing, and they would go on believing the
        // server was doing what they typed.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { ItemsAtOnce = asked, ThreadsPerItem = asked },
            processorCount: 8);

        Assert.Equal(ConcurrencyCap.Default, load.InForce.ItemsAtOnce);
        Assert.Equal(ThreadCount.DefaultFor(8), load.InForce.ThreadsPerItem);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(-1)]
    public void The_complaint_names_the_number_that_was_typed_and_what_runs_instead(int asked)
    {
        // A refusal that does not repeat the number leaves an operator comparing a
        // page against their own memory of what they typed, and one that does not say
        // what is in force leaves them assuming it is theirs.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { ItemsAtOnce = asked },
            processorCount: 8);

        var complaint = Assert.Single(load.Complaints);

        Assert.Contains(
            asked.ToString(CultureInfo.InvariantCulture),
            complaint.Problem,
            StringComparison.Ordinal);

        Assert.Contains(
            ConcurrencyCap.Default.ToString(CultureInfo.InvariantCulture),
            complaint.InForce,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Both_limits_survive_the_serializer_the_server_stores_them_with()
    {
        // The server persists this type with XmlSerializer, and a value that does not
        // survive the round trip is a saved setting an operator watches come back
        // empty with nothing in any log.
        var serializer = new XmlSerializer(typeof(PluginConfiguration));

        using var written = new StringWriter(CultureInfo.InvariantCulture);
        serializer.Serialize(written, new PluginConfiguration { ItemsAtOnce = 3, ThreadsPerItem = 5 });

        using var read = new StringReader(written.ToString());

        // CA5369 asks for the XmlReader overload with DTD processing off. The subject
        // here is the call the server makes, on a string this test wrote a line
        // earlier.
#pragma warning disable CA5369
        var restored = Assert.IsType<PluginConfiguration>(serializer.Deserialize(read));
#pragma warning restore CA5369

        Assert.Equal(3, restored.ItemsAtOnce);
        Assert.Equal(5, restored.ThreadsPerItem);
    }

    [Theory]
    [InlineData(nameof(PluginConfiguration.ItemsAtOnce))]
    [InlineData(nameof(PluginConfiguration.ThreadsPerItem))]
    public void The_page_reads_and_writes_each_limit(string setting)
    {
        // ConfigurationShellTests compares the two name sets and would catch a
        // setting reaching no page at all. This names which, so a limit silently
        // swapped for the other one on the page is a failure here rather than a set
        // that still matches.
        var page = ConfigurationPageSource.Markup();

        Assert.Matches(new Regex(@"\bconfig\." + setting + @"\b", RegexOptions.None, TimeSpan.FromSeconds(5)), page);
        Assert.Matches(new Regex(@"\bconfig\." + setting + @"\s*=", RegexOptions.None, TimeSpan.FromSeconds(5)), page);
        Assert.Contains("id=\"" + setting + "\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void The_page_says_what_reads_each_limit_exactly_while_something_does()
    {
        // A field an operator types into is read as a field that does something.
        // Something reads both now, and it is not a run: the dry run reports what a
        // run would cost and transcribes nothing. So the page names that reader, and
        // the sentence is read rather than trusted, in both directions.
        //
        // The loud direction is the page naming a reader that has gone, which turns a
        // disclosure into a lie at exactly the change least likely to remember a page
        // two directories away. The quiet direction is the sentence being dropped
        // while something still reads either number, which leaves two fields whose
        // effect an operator has to find out by experiment.
        var reading = FilesOutsideTheConfigurationThatNameALimit();
        var says = ConfigurationPageSource.Markup().Contains(ReadByTheDryRun, StringComparison.Ordinal);

        Assert.True(
            !says || reading.Count > 0,
            $"the configuration page says \"{ReadByTheDryRun}\" and no file outside Configuration/ names either limit, so the page credits a reader this tree does not hold.");

        Assert.True(
            says || reading.Count == 0,
            $"{reading.Count} file(s) outside Configuration/ read a limit: {string.Join(", ", reading)}. The configuration page no longer says what reads them, so two fields an operator types into say nothing about what they change.");
    }

    /// <summary>
    /// The plugin source files outside the configuration that name either limit, by
    /// path relative to the project.
    /// </summary>
    /// <remarks>
    /// Read off the checkout rather than off the assembly, because a mention in a
    /// remark counts: the question is whether anything has started carrying these
    /// numbers anywhere, and a file naming one in prose is a file where somebody
    /// has begun.
    ///
    /// The configuration directory is excluded because that is where the settings
    /// are declared, validated and resolved, and every one of those names them by
    /// definition. What the sentence is about is a reader BEYOND that: a run, a
    /// backend, a composition root.
    ///
    /// The population is the plugin project rather than the tree, so a test double
    /// naming a limit is not the plugin using one, and so this file is outside what
    /// it reads.
    /// </remarks>
    private static List<string> FilesOutsideTheConfigurationThatNameALimit()
    {
        var project = Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.WhisperSubtitles");

        Assert.True(Directory.Exists(project), $"the plugin project was not found at {project}");

        var configuration = Path.Combine(project, "Configuration");
        var sources = Directory.GetFiles(project, "*.cs", SearchOption.AllDirectories);

        Assert.NotEmpty(sources);

        return sources
            .Where(path => !path.StartsWith(configuration, StringComparison.Ordinal))
            .Where(path =>
            {
                var text = File.ReadAllText(path);

                return text.Contains(nameof(PluginConfiguration.ItemsAtOnce), StringComparison.Ordinal)
                    || text.Contains(nameof(PluginConfiguration.ThreadsPerItem), StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(project, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;

    [Fact]
    public void The_page_offers_the_sentinel_the_code_reads_as_nobody_choosing()
    {
        // The page and the rules have to mean the same thing by zero. A page sending
        // its own idea of unset would save a number the rules refuse, and the operator
        // would meet a complaint about a field they never typed in.
        Assert.Contains(
            "letTheMachineDecide: " + ConfigurationValidation.LetTheMachineDecide.ToString(CultureInfo.InvariantCulture),
            ConfigurationPageSource.Markup(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_sentinel_is_a_value_neither_limit_would_ever_accept()
    {
        // What makes the sentinel safe rather than convenient: no number of items and
        // no number of threads is zero, so it cannot collide with a value an operator
        // meant. Asked of the rules rather than asserted about the constant.
        Assert.False(ConcurrencyCap.Choose(ConfigurationValidation.LetTheMachineDecide, 8).IsAccepted);
        Assert.False(ThreadCount.Choose(ConfigurationValidation.LetTheMachineDecide, 8).IsAccepted);
    }

    [Theory]
    [InlineData(nameof(PluginConfiguration.ItemsAtOnce))]
    [InlineData(nameof(PluginConfiguration.ThreadsPerItem))]
    public void Each_limit_is_a_setting_a_rule_decides(string setting)
    {
        // ConfigurationValidationTests compares the whole set both ways. This names
        // the two, so a change dropping a limit from the list and its property in one
        // edit is visible as this failing rather than as a set that agrees about less.
        Assert.Contains(setting, ConfigurationValidation.ValidatedFields);
    }

    [Fact]
    public void A_file_this_release_stands_back_from_still_leaves_a_run_it_could_make()
    {
        // A configuration written by a newer release is not read field by field, so
        // its settings come from a second construction of the defaults. That one has
        // to produce numbers a run can act on too, and it is the one nobody looks at.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { SchemaVersion = ConfigurationValidation.CurrentSchemaVersion + 1 },
            processorCount: 8);

        Assert.Single(load.Complaints);
        Assert.Equal(ConcurrencyCap.Default, load.InForce.ItemsAtOnce);
        Assert.Equal(ThreadCount.DefaultFor(8), load.InForce.ThreadsPerItem);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 0)]
    [InlineData(-1, 4)]
    public void The_settings_a_run_reads_refuse_a_limit_that_is_not_a_number_of_anything(
        int items,
        int threads)
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
            items,
            threads,
            ConfigurationValidation.NoPathNamed,
            ConfigurationValidation.NoPathNamed));
    }

    [Fact]
    public void A_file_that_will_not_parse_at_all_leaves_a_run_it_could_make()
    {
        // The third construction of the defaults, reached from ConfigurationFile
        // rather than from the rules, and the one a reader of the rules never meets.
        var load = ConfigurationFile.Read("<PluginConfiguration><SchemaVersion>not a number");

        Assert.NotEmpty(load.Complaints);
        Assert.True(load.InForce.ItemsAtOnce >= 1);
        Assert.True(load.InForce.ThreadsPerItem >= 1);
    }
}
