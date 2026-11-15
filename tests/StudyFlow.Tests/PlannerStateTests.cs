using Microsoft.JSInterop;
using StudyFlow.Core;

namespace StudyFlow.Tests;

public sealed class PlannerStateTests
{
    [Fact]
    public async Task LoadsAndSavesThroughTheBrowserStorageContract()
    {
        var browser = new BrowserStorageStub();
        var state = new PlannerState(browser);
        await state.LoadAsync();
        var expected = WorkspaceRules.ChangeBudget(state.Workspace, 50, false);

        await state.SaveAsync(expected);
        var reloaded = new PlannerState(browser);
        await reloaded.LoadAsync();

        Assert.Equal(50, reloaded.Workspace.DailyMinutes);
        Assert.Equal(new DateOnly(2026, 11, 16), reloaded.Today);
        Assert.Equal(50, reloaded.Plan.Days[0].Budget);
    }

    [Fact]
    public async Task StaleAndFailedWritesPreserveTheCurrentView()
    {
        var browser = new BrowserStorageStub();
        var state = new PlannerState(browser);
        await state.LoadAsync();
        var next = WorkspaceRules.ChangeBudget(state.Workspace, 45, false);
        browser.Raw = WorkspaceJson.Write(Workspace.Empty);

        var conflict = await Assert.ThrowsAsync<PlannerRuleException>(() => state.SaveAsync(next));
        Assert.Contains("Another tab", conflict.Message);
        Assert.Equal(90, state.Workspace.DailyMinutes);
        Assert.NotEqual(WorkspaceJson.Write(next), browser.Raw);

        await state.LoadAsync();
        browser.Fail = true;
        var storageError = await Assert.ThrowsAsync<PlannerRuleException>(() => state.SaveAsync(next));
        Assert.Contains("could not save", storageError.Message);
        Assert.Equal(90, state.Plan.Days[0].Budget);
    }

    [Fact]
    public async Task CorruptStorageCanBeRecoveredThenReset()
    {
        var browser = new BrowserStorageStub { Raw = "{bad" };
        var state = new PlannerState(browser);
        await state.LoadAsync();
        Assert.NotNull(state.LoadError);
        Assert.True(state.HasRecoveryData);
        await state.DownloadAsync();
        Assert.Equal(new object?[] { "{bad", "studyflow-recovery.json" }, browser.Download);

        await state.SaveAsync(Workspace.Empty);
        Assert.Null(state.LoadError);
        Assert.Empty(WorkspaceJson.Read(browser.Raw!).Assignments);
    }

    private sealed class BrowserStorageStub : IJSRuntime
    {
        public string? Raw { get; set; }
        public object?[]? Download { get; private set; }
        public bool Fail { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, default, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            object? result;
            switch (identifier)
            {
                case "studyFlow.read":
                    result = new BrowserSnapshot("2026-11-16", Raw);
                    break;
                case "studyFlow.save":
                    if (Fail)
                        throw new JSException("Storage full");
                    var matches = string.Equals(Raw, (string?)args![0], StringComparison.Ordinal);
                    if (matches)
                        Raw = (string)args[1]!;
                    result = matches;
                    break;
                case "studyFlow.download":
                    Download = args;
                    result = default(TValue);
                    break;
                default:
                    throw new InvalidOperationException(identifier);
            }

            return ValueTask.FromResult((TValue)result!);
        }
    }
}
