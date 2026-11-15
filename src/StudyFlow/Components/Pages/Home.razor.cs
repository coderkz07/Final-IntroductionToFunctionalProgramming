using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using StudyFlow.Core;

namespace StudyFlow.Components.Pages;

public partial class Home : IDisposable
{
    [Inject] private PlannerState State { get; set; } = default!;

    private enum DialogKind { None, Assignment, Log, Settings, DeleteAssignment, DeleteSession, Restore, Reset }

    private bool loaded;
    private bool busy;
    private string? notice;
    private string? pageError;
    private string? dialogError;
    private DialogKind dialog;

    private Guid? selectedId;
    private Guid logId;
    private Guid selectedSession;
    private string title = "";
    private string subject = "";
    private DateOnly due;
    private ElementReference dueInput;
    private ElementReference logDateInput;
    private int estimate;
    private int logMinutes;
    private int budget;
    private bool weekends;
    private Priority priority;
    private Workspace? backup;

    private string query = "";
    private string filter = "all";
    private int historyCount = 15;
    private HistoryEntry[] History = [];

    private sealed record HistoryEntry(StudySession Session, string Title);

    private string View => Navigation.ToBaseRelativePath(Navigation.Uri).Split('?', '#')[0].Trim('/').ToLowerInvariant();
    private string Heading => View switch { "assignments" => "Assignments", "schedule" => "Schedule", _ => "Dashboard" };

    private bool Ready => loaded && State.LoadError is null;
    private string IsoToday => FormatDate(State.Today);
    private int OpenCount => State.Plan.Assignments.Count(item => !item.IsComplete);
    private int OverdueCount => State.Plan.Assignments.Count(item => !item.IsComplete && item.Assignment.Due < State.Today);
    private int DueSoon => State.Plan.Assignments.Count(item => !item.IsComplete
        && item.Assignment.Due >= State.Today && item.Assignment.Due < State.Today.AddDays(7));
    private int Remaining => State.Plan.Assignments.Sum(item => item.Remaining);
    private AssignmentProgress[] Upcoming => State.Plan.Assignments.Where(item => !item.IsComplete).Take(5).ToArray();

    private AssignmentProgress[] Filtered => State.Plan.Assignments.Where(item =>
        (item.Assignment.Title.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)
            || item.Assignment.Subject.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
        && (filter switch
        {
            "open" => !item.IsComplete,
            "overdue" => !item.IsComplete && item.Assignment.Due < State.Today,
            "complete" => item.IsComplete,
            _ => true
        })).ToArray();

    private string DialogTitle => dialog switch
    {
        DialogKind.Assignment => selectedId is null ? "New assignment" : "Edit assignment",
        DialogKind.Log => "Log study time",
        DialogKind.Settings => "Planning settings",
        DialogKind.DeleteAssignment => "Delete assignment?",
        DialogKind.DeleteSession => "Remove study session?",
        DialogKind.Restore => "Restore this backup?",
        _ => "Reset workspace storage?"
    };

    private string ConfirmationText => dialog switch
    {
        DialogKind.DeleteAssignment => "This also deletes the sessions you logged for this assignment.",
        DialogKind.DeleteSession => "This adds the session’s minutes back to the work left.",
        DialogKind.Restore => "This replaces the plan and study history saved in this browser.",
        _ => "This clears your saved plan and starts a new one."
    };

    private string ConfirmLabel => dialog switch
    {
        DialogKind.DeleteAssignment => "Delete assignment",
        DialogKind.DeleteSession => "Remove session",
        DialogKind.Restore => "Restore backup",
        _ => "Reset workspace"
    };

    protected override void OnInitialized() => Navigation.LocationChanged += OnLocationChanged;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args) => _ = InvokeAsync(StateHasChanged);

    public void Dispose() => Navigation.LocationChanged -= OnLocationChanged;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        await ReloadAsync();
        StateHasChanged();
    }

    private async Task ReloadAsync()
    {
        busy = true;
        pageError = null;
        try
        {
            await State.LoadAsync();
            UpdateHistory();
            loaded = true;
        }
        finally
        {
            busy = false;
        }
    }

    private void UpdateHistory()
    {
        var titles = State.Workspace.Assignments.ToDictionary(assignment => assignment.Id, assignment => assignment.Title);
        History = State.Workspace.Sessions.Reverse().OrderByDescending(session => session.Date)
            .Select(session => new HistoryEntry(session, titles[session.AssignmentId])).ToArray();
    }

    private static string FormatMinutes(int minutes)
    {
        if (minutes < 60)
            return $"{minutes} min";

        return minutes % 60 == 0 ? $"{minutes / 60} h" : $"{minutes / 60} h {minutes % 60} min";
    }

    private void Open(DialogKind kind)
    {
        dialogError = null;
        pageError = null;
        notice = null;
        dialog = kind;
    }

    private async Task CloseAsync()
    {
        await Js.InvokeVoidAsync("studyFlow.closeDialog");
        dialog = DialogKind.None;
        dialogError = null;
        backup = null;
    }

    private void EditAssignment(Assignment assignment) => OpenAssignment(assignment);

    private void OpenAssignment(Assignment? assignment)
    {
        selectedId = assignment?.Id;
        title = assignment?.Title ?? "";
        subject = assignment?.Subject ?? "";
        due = assignment?.Due ?? State.Today.AddDays(1);
        estimate = assignment?.EstimatedMinutes ?? 60;
        priority = assignment?.Priority ?? Priority.Normal;
        Open(DialogKind.Assignment);
    }

    private void OpenLog()
    {
        var first = State.Plan.Assignments.First(item => !item.IsComplete);
        logId = first.Assignment.Id;
        logMinutes = Math.Min(25, first.Remaining);
        Open(DialogKind.Log);
    }

    private void OpenSettings()
    {
        budget = State.Workspace.DailyMinutes;
        weekends = State.Workspace.IncludeWeekends;
        Open(DialogKind.Settings);
    }

    private void ConfirmAssignment() => Open(DialogKind.DeleteAssignment);

    private void ConfirmSession(Guid id)
    {
        selectedSession = id;
        Open(DialogKind.DeleteSession);
    }

    private Task SaveAssignmentAsync() => ApplyAsync(async () => WorkspaceRules.SaveAssignment(State.Workspace,
        selectedId, title, subject, await ReadDateAsync(dueInput), estimate, priority), "Assignment saved.");

    private Task LogAsync() => ApplyAsync(async () => WorkspaceRules.Log(State.Workspace, logId,
        await ReadDateAsync(logDateInput), logMinutes, State.Today), "Study session logged.");

    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // Read the normalized native value when the form is submitted across browsers.
    private async Task<DateOnly> ReadDateAsync(ElementReference input)
    {
        var value = await Js.InvokeAsync<string>("studyFlow.dateValue", input);
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new PlannerRuleException("Choose a valid date.");

        return date;
    }

    private Task SaveSettingsAsync() => ApplyAsync(() => WorkspaceRules.ChangeBudget(State.Workspace,
        budget, weekends), "Settings saved.");

    private Task LoadExampleAsync() => ApplyAsync(() => ExamplePlan.Build(State.Today), "Example plan loaded.");

    private Task ConfirmAsync() => dialog switch
    {
        DialogKind.DeleteAssignment => ApplyAsync(() => WorkspaceRules.DeleteAssignment(State.Workspace, selectedId!.Value), "Assignment deleted."),
        DialogKind.DeleteSession => ApplyAsync(() => WorkspaceRules.DeleteSession(State.Workspace, selectedSession), "Study session removed."),
        DialogKind.Restore => ApplyAsync(() => backup!, "Backup restored."),
        _ => ApplyAsync(() => Workspace.Empty, "Workspace reset.")
    };

    private Task ApplyAsync(Func<Workspace> change, string message) =>
        ApplyAsync(() => Task.FromResult(change()), message);

    private async Task ApplyAsync(Func<Task<Workspace>> change, string message)
    {
        if (busy)
            return;

        busy = true;
        notice = null;
        pageError = null;
        dialogError = null;
        try
        {
            await State.SaveAsync(await change());
            UpdateHistory();
            if (dialog != DialogKind.None)
                await CloseAsync();

            notice = message;
        }
        catch (PlannerRuleException exception)
        {
            ReportError(exception.Message);
        }
        catch (JSException)
        {
            ReportError("Reload the page and try again.");
        }
        finally
        {
            busy = false;
        }
    }

    private async Task ChooseBackupAsync(InputFileChangeEventArgs args)
    {
        if (busy)
            return;

        busy = true;
        dialogError = null;
        try
        {
            await using var stream = args.File.OpenReadStream(WorkspaceJson.MaxBytes);
            using var reader = new StreamReader(stream);
            backup = WorkspaceJson.Read(await reader.ReadToEndAsync());
            Open(DialogKind.Restore);
        }
        catch (PlannerRuleException exception)
        {
            ReportError(exception.Message);
        }
        catch (IOException)
        {
            ReportError("Choose a StudyFlow backup under 1 MB.");
        }
        finally
        {
            busy = false;
        }
    }

    private async Task DownloadAsync()
    {
        try
        {
            await State.DownloadAsync();
        }
        catch (JSException)
        {
            ReportError("Couldn’t download the backup. Check your browser settings.");
        }
    }

    private void ReportError(string error)
    {
        if (dialog == DialogKind.None)
            pageError = error;
        else
            dialogError = error;
    }
}
