namespace StudyFlow.Core;

public static class WorkspaceRules
{
    public const int MaxAssignments = 200;
    public const int MaxSessions = 2_000;
    public static readonly DateOnly Earliest = new(2000, 1, 1);
    public static readonly DateOnly Latest = new(2100, 12, 31);

    public static Workspace SaveAssignment(Workspace workspace, Guid? id, string title,
        string subject, DateOnly due, int estimate, Priority priority)
    {
        var assignment = new Assignment(id ?? Guid.NewGuid(), title.Trim(), subject.Trim(),
            due, estimate, priority);
        ValidateAssignment(assignment);

        if (id is null)
        {
            Require(workspace.Assignments.Length < MaxAssignments,
                $"Keep at most {MaxAssignments} assignments in this workspace.");
            return workspace with { Assignments = workspace.Assignments.Add(assignment) };
        }

        var original = FindAssignment(workspace, id.Value);
        var logged = workspace.Sessions.Where(session => session.AssignmentId == id).Sum(session => session.Minutes);
        Require(estimate >= logged, $"The estimate must be at least the {logged} minutes already logged.");
        return workspace with { Assignments = workspace.Assignments.Replace(original, assignment) };
    }

    public static Workspace DeleteAssignment(Workspace workspace, Guid id)
    {
        var assignment = FindAssignment(workspace, id);
        return workspace with
        {
            Assignments = workspace.Assignments.Remove(assignment),
            Sessions = workspace.Sessions.RemoveAll(session => session.AssignmentId == id)
        };
    }

    public static Workspace Log(Workspace workspace, Guid assignmentId, DateOnly date,
        int minutes, DateOnly today)
    {
        var assignment = FindAssignment(workspace, assignmentId);
        var session = new StudySession(Guid.NewGuid(), assignmentId, date, minutes);
        ValidateSession(session);
        Require(date <= today, "Use today or an earlier date for a completed session.");
        Require(workspace.Sessions.Length < MaxSessions, $"Keep at most {MaxSessions} sessions in this workspace.");
        var logged = workspace.Sessions.Where(entry => entry.AssignmentId == assignmentId)
            .Sum(session => session.Minutes);
        Require(minutes <= assignment.EstimatedMinutes - logged,
            $"This assignment has {assignment.EstimatedMinutes - logged} minutes left.");
        return workspace with { Sessions = workspace.Sessions.Add(session) };
    }

    public static Workspace DeleteSession(Workspace workspace, Guid id)
    {
        Require(workspace.Sessions.Any(session => session.Id == id), "This session no longer exists.");
        return workspace with { Sessions = workspace.Sessions.RemoveAll(session => session.Id == id) };
    }

    public static Workspace ChangeBudget(Workspace workspace, int minutes, bool includeWeekends)
    {
        ValidateBudget(minutes);
        return workspace with { DailyMinutes = minutes, IncludeWeekends = includeWeekends };
    }

    public static void Validate(Workspace workspace)
    {
        Require(workspace.Version == Workspace.CurrentVersion, "This backup uses an unsupported file version.");
        ValidateBudget(workspace.DailyMinutes);
        Require(!workspace.Assignments.IsDefault && workspace.Assignments.Length <= MaxAssignments,
            "The assignment list is missing or too large.");
        Require(!workspace.Sessions.IsDefault && workspace.Sessions.Length <= MaxSessions,
            "The session list is missing or too large.");

        var estimates = new Dictionary<Guid, int>();
        foreach (var assignment in workspace.Assignments)
        {
            if (assignment is null)
                throw new PlannerRuleException("The backup contains a missing assignment.");

            ValidateAssignment(assignment);
            Require(estimates.TryAdd(assignment.Id, assignment.EstimatedMinutes), "The backup contains duplicate assignments.");
        }

        var ids = new HashSet<Guid>();
        foreach (var session in workspace.Sessions)
        {
            if (session is null)
                throw new PlannerRuleException("The backup contains a missing session.");

            ValidateSession(session);
            Require(ids.Add(session.Id), "The backup contains duplicate sessions.");
            Require(estimates.TryGetValue(session.AssignmentId, out var remaining),
                "A session refers to an assignment that is missing.");
            Require(session.Minutes <= remaining, "Logged time exceeds the assignment estimate.");
            estimates[session.AssignmentId] = remaining - session.Minutes;
        }
    }

    private static Assignment FindAssignment(Workspace workspace, Guid id) =>
        workspace.Assignments.FirstOrDefault(assignment => assignment.Id == id)
        ?? throw new PlannerRuleException("This assignment no longer exists.");

    private static void ValidateAssignment(Assignment assignment)
    {
        Require(assignment.Id != Guid.Empty, "The assignment identifier is missing.");
        Require(!string.IsNullOrWhiteSpace(assignment.Title) && assignment.Title.Length <= 100,
            "Enter an assignment title of 1–100 characters.");
        Require(!string.IsNullOrWhiteSpace(assignment.Subject) && assignment.Subject.Length <= 48,
            "Enter a subject of 1–48 characters.");
        Require(assignment.Due >= Earliest && assignment.Due <= Latest, "Choose a due date from 2000 through 2100.");
        Require(assignment.EstimatedMinutes is >= 15 and <= 2400, "Enter an estimate from 15 to 2,400 minutes.");
        Require(Enum.IsDefined(assignment.Priority), "Choose a valid priority.");
    }

    private static void ValidateSession(StudySession session)
    {
        Require(session.Id != Guid.Empty, "The session identifier is missing.");
        Require(session.Date >= Earliest && session.Date <= Latest, "Choose a session date from 2000 through 2100.");
        Require(session.Minutes is >= 1 and <= 480, "Log between 1 and 480 minutes per session.");
    }

    private static void ValidateBudget(int minutes) =>
        Require(minutes is >= 15 and <= 480, "Set a daily budget from 15 to 480 minutes.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new PlannerRuleException(message);
    }
}
