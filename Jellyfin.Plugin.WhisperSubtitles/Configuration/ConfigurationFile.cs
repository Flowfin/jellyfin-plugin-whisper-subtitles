using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Jellyfin.Plugin.WhisperSubtitles.Configuration;

/// <summary>
/// Turns the text sitting on disk into settings, including when that text is not
/// a configuration at all.
/// </summary>
/// <remarks>
/// Apart from <see cref="ConfigurationValidation"/> because this half touches a
/// serializer and that half decides values from its arguments, and only one of the
/// two is held to the coverage floor for pure logic.
///
/// The server does its own deserialisation and replaces a file it cannot read with
/// a default one. This exists for the same bytes read deliberately: a test that
/// feeds a truncated file needs an answer rather than an exception, and #41 will
/// need to read a file written by a version this one does not know.
/// </remarks>
public static class ConfigurationFile
{
    private static readonly XmlSerializer _serializer = new(typeof(PluginConfiguration));

    /// <summary>
    /// Reads configuration text.
    /// </summary>
    /// <param name="xml">Whatever the file held.</param>
    /// <returns>The settings in force and every value that was refused.</returns>
    /// <remarks>
    /// A file that will not parse is one complaint and the documented defaults,
    /// never an exception. This is read on a server start, and a plugin that threw
    /// here would be a plugin reported as broken with no page to repair it from,
    /// which is worse than one that runs and transcribes nothing.
    /// </remarks>
    public static ConfigurationLoad Read(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return Unreadable("it is empty.");
        }

        var settings = new XmlReaderSettings
        {
            // The file is the operator's and the plugin is not the only thing that
            // reads it, but a document type declaration has no business in a
            // settings file and resolving one would let it name something off this
            // machine.
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };

        try
        {
            using var text = new StringReader(xml);
            using var reader = XmlReader.Create(text, settings);

            return ConfigurationValidation.Of(_serializer.Deserialize(reader) as PluginConfiguration);
        }
        catch (InvalidOperationException thrown)
        {
            // What XmlSerializer wraps every parse failure in. The inner exception
            // is the one that says where, and it is the half worth showing.
            return Unreadable((thrown.InnerException ?? thrown).Message);
        }
        catch (XmlException thrown)
        {
            return Unreadable(thrown.Message);
        }
    }

    private static ConfigurationLoad Unreadable(string reason) =>
        new(
            new SettingsInForce(
                ConfigurationValidation.CurrentSchemaVersion,
                ConfigurationValidation.NoBackendChosen,
                ConfigurationValidation.NoTargetLanguage,
                new Dictionary<Guid, string>()),
            [
                new SettingComplaint(
                    "configuration",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "the file could not be read: {0}",
                        reason),
                    "Every setting is at its default and nothing is transcribed."),
            ]);
}
