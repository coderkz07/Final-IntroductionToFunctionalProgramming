using System.Collections.Immutable;

namespace StudyFlow.Core;

public enum Priority { Low, Normal, High }

public sealed record Assignment(Guid Id, string Title, string Subject, DateOnly Due,
    int EstimatedMinutes, Priority Priority);

public sealed record StudySession(Guid Id, Guid AssignmentId, DateOnly Date, int Minutes);

public sealed record Workspace(int Version, int DailyMinutes, bool IncludeWeekends,
    ImmutableArray<Assignment> Assignments, ImmutableArray<StudySession> Sessions)
{
    public const int CurrentVersion = 1;
    public static Workspace Empty { get; } = new(CurrentVersion, 90, true, [], []);
}

public sealed class PlannerRuleException(string message) : Exception(message);
