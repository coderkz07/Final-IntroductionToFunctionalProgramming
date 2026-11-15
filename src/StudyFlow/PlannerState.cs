using System.Globalization;
using Microsoft.JSInterop;
using StudyFlow.Core;

namespace StudyFlow;

public sealed record BrowserSnapshot(string Today, string? Raw);

public sealed class PlannerState(IJSRuntime js)
{
    private string? saved;
    private bool readable;

    public Workspace Workspace { get; private set; } = Workspace.Empty;
    public DateOnly Today { get; private set; }
    public StudyPlan Plan { get; private set; } = Scheduler.Build(Workspace.Empty, new(2000, 1, 1));
    public string? LoadError { get; private set; }
    public bool HasRecoveryData => readable && saved is not null;

    public async Task LoadAsync()
    {
        readable = false;
        LoadError = null;

        try
        {
            var snapshot = await js.InvokeAsync<BrowserSnapshot>("studyFlow.read");
            saved = snapshot.Raw;
            Today = DateOnly.ParseExact(snapshot.Today, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            readable = true;
            SetWorkspace(saved is null ? Workspace.Empty : WorkspaceJson.Read(saved));
        }
        catch (PlannerRuleException exception)
        {
            LoadError = exception.Message;
        }
        catch (JSException)
        {
            LoadError = "This browser could not open local storage. Check its site settings, then try again.";
        }
    }

    public async Task SaveAsync(Workspace next)
    {
        if (!readable)
            throw new PlannerRuleException("Local storage is unavailable. Reload the workspace and try again.");

        WorkspaceRules.Validate(next);
        var json = WorkspaceJson.Write(next);

        try
        {
            if (!await js.InvokeAsync<bool>("studyFlow.save", saved, json))
                throw new PlannerRuleException("Another tab changed the plan. Reload this page before saving again.");
        }
        catch (JSException)
        {
            throw new PlannerRuleException("The browser could not save your changes. Free some space or check site settings.");
        }

        saved = json;
        LoadError = null;
        SetWorkspace(next);
    }

    public ValueTask DownloadAsync() => js.InvokeVoidAsync("studyFlow.download",
        LoadError is null ? WorkspaceJson.Write(Workspace) : saved,
        LoadError is null ? "studyflow-backup.json" : "studyflow-recovery.json");

    private void SetWorkspace(Workspace workspace)
    {
        Workspace = workspace;
        Plan = Scheduler.Build(workspace, Today);
    }
}
