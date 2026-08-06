using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;

// This file is the fixture set for the isolation check: a type inside the backend
// namespace, the mistake outside it, and the one-change neighbour. What the check
// reads is where each type sits, so the three have to be in different namespaces
// and they have to be read together to be understood. Splitting them across three
// files by name would separate the fixture from the neighbour it is only
// meaningful beside.
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1403 // File may only contain a single namespace
#pragma warning disable SA1649 // File name should match first type name

namespace Jellyfin.Plugin.WhisperSubtitles.Tests.Fixtures.Backends
{
    /// <summary>
    /// A concrete backend that exists so the isolation check has something to find.
    /// It transcribes nothing and lives only in the test assembly.
    /// </summary>
    internal sealed class FixtureBackend : ITranscriptionBackend
    {
        public BackendDescription Description => new(
            "fixture",
            new List<string> { "none" },
            new List<string> { "en" },
            canDetectLanguage: false,
            cancellationBudget: TimeSpan.FromSeconds(1));

        public Task<BackendReadiness> CheckReadinessAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new BackendReadiness(false, "fixture"));

        public CostEstimate EstimateCost(TimeSpan mediaDuration) =>
            new(TimeSpan.Zero, TimeSpan.Zero);

        public Task<TranscriptionResult> TranscribeAsync(
            TranscriptionRequest request,
            IProgress<double> progress,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The fixture backend exists to be found, not to be run.");
    }
}

namespace Jellyfin.Plugin.WhisperSubtitles.Tests.Fixtures.Outside
{
    using Jellyfin.Plugin.WhisperSubtitles.Tests.Fixtures.Backends;

    /// <summary>
    /// The mistake the isolation check exists to refuse: a type outside the backend
    /// folder that names a concrete backend in its own signature.
    /// </summary>
    internal sealed class HoldsAConcreteBackend
    {
        public FixtureBackend? Backend { get; set; }
    }

    /// <summary>
    /// The one-change neighbour. Identical except that it names the interface, which
    /// is what every type outside the backend folder is supposed to do.
    /// </summary>
    internal sealed class HoldsTheInterface
    {
        public ITranscriptionBackend? Backend { get; set; }
    }
}
