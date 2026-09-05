using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Api;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The one path this plugin answers on the server, judged the way the server
/// meets it: what it declares, who may ask, and whether the server can build it
/// out of what the registrator registered.
/// </summary>
/// <remarks>
/// No server is booted here and none of these says one was. What a booted server
/// answers on this path is read by the scan in <c>booted-server.yml</c>, which
/// requires every route the claim record names to be in the server's own route
/// document, and the record is compared against this type's attributes by
/// <c>ClaimRecordTests</c>. What is left for here is the declaration and the
/// construction, which are the two ways a route can be there and not answer.
///
/// The remote half is judged at the question rather than here, because the
/// controller takes the handler the composition root registers and that one owns
/// a real socket pool; the question takes any handler, and
/// <c>ReadinessQuestionTests</c> drives it through a stub endpoint.
/// </remarks>
public sealed class ReadinessControllerTests
{
    private const string Tool = "/opt/whisper/whisper-cli";

    private const string Model = "/var/lib/models/ggml-base.bin";

    [Fact]
    public void The_path_it_declares_is_the_one_the_record_and_the_page_spell()
    {
        // Prefix and method template, joined the way the server's route document
        // spells a path, so the constant the page and the record are held to is the
        // path the server actually registers rather than a string beside it.
        var prefix = Assert.Single(typeof(ReadinessController).GetCustomAttributes<RouteAttribute>()).Template;
        var method = Assert.Single(Action().GetCustomAttributes<HttpMethodAttribute>());

        Assert.Equal(ReadinessController.ReadinessPath, "/" + prefix + "/" + method.Template);
    }

    [Fact]
    public void It_answers_a_post_and_no_other_method()
    {
        // The question carries the settings it is about and one of them is a key, so
        // it goes in a body. A GET here would be a key in a URL.
        var method = Assert.Single(Action().GetCustomAttributes<HttpMethodAttribute>());

        Assert.Equal(new[] { "POST" }, method.HttpMethods);
    }

    [Fact]
    public void Only_an_elevated_session_may_ask()
    {
        // The same policy the server puts on reading a plugin's configuration. The
        // answer names a path an operator typed and says whether a file is at it.
        var policy = Assert.Single(typeof(ReadinessController).GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal(Policies.RequiresElevation, policy.Policy);
        Assert.Empty(Action().GetCustomAttributes<AllowAnonymousAttribute>());
    }

    [Fact]
    public void The_settings_come_out_of_the_body()
    {
        var settings = Assert.Single(Action().GetParameters(), parameter => parameter.ParameterType == typeof(PluginConfiguration));

        Assert.NotEmpty(settings.GetCustomAttributes<FromBodyAttribute>());
    }

    [Fact]
    public void The_server_can_build_it_from_what_the_registrator_registers()
    {
        // The server activates a plugin's controllers out of its own container, the
        // way it builds the scheduled task, so a constructor argument nothing
        // registered is a route that is declared and answers with an error.
        using var provider = Registered();

        Assert.NotNull(ActivatorUtilities.CreateInstance<ReadinessController>(provider));
    }

    [Fact]
    public void Without_the_registration_the_same_construction_fails()
    {
        // The near miss: the same call against a container the registrator never
        // wrote into, which is what says the registration is what carries it.
        using var bare = new ServiceCollection().BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(
            () => ActivatorUtilities.CreateInstance<ReadinessController>(bare));
    }

    [Fact]
    public async Task It_answers_with_what_the_question_answers()
    {
        var files = StubFileFacts.Empty().WithTool(Tool).WithModel(Model);
        using var http = new RemoteHttpHandler();
        var controller = new ReadinessController(ScriptedProcessRunner.Starting(ScriptedProcess.Printing([])), files, http);

        var answer = await controller.AskAsync(
            new PluginConfiguration
            {
                Backend = LocalWhisperBackend.BackendName,
                LocalToolPath = Tool,
                LocalModelPath = Model,
            },
            CancellationToken.None).ConfigureAwait(true);

        var ok = Assert.IsType<OkObjectResult>(answer.Result);
        var report = Assert.IsType<ReadinessReport>(ok.Value);

        Assert.True(report.IsReady);
        Assert.Equal(LocalWhisperBackend.BackendName, report.Backend);
        Assert.Equal(new[] { Tool, Model }, files.Asked);
    }

    [Fact]
    public void The_seams_are_required()
    {
        var files = StubFileFacts.Empty();
        using var http = new RemoteHttpHandler();
        var runner = ScriptedProcessRunner.Starting(ScriptedProcess.Printing([]));

        Assert.Throws<ArgumentNullException>(() => new ReadinessController(null!, files, http));
        Assert.Throws<ArgumentNullException>(() => new ReadinessController(runner, null!, http));
        Assert.Throws<ArgumentNullException>(() => new ReadinessController(runner, files, null!));
    }

    private static MethodInfo Action() =>
        typeof(ReadinessController).GetMethod(nameof(ReadinessController.AskAsync))!;

    private static ServiceProvider Registered()
    {
        var services = new ServiceCollection();

        new PluginServiceRegistrator().RegisterServices(services, null!);

        return services.BuildServiceProvider();
    }
}
