using System.Collections.Immutable;

namespace StudyFlow.Core;

public sealed record AssignmentProgress(Assignment Assignment, int Logged)
{
    public int Remaining => Assignment.EstimatedMinutes - Logged;
    public bool IsComplete => Remaining == 0;
}

public sealed record StudyBlock(Assignment Assignment, int Minutes);

public sealed record StudyDay(DateOnly Date, bool Enabled, int Budget, int Logged, ImmutableArray<StudyBlock> Blocks)
{
    public int Planned => Blocks.Sum(block => block.Minutes);
}

public enum GapReason { Overdue, DeadlineCapacity, BeyondWindow }

public sealed record PlanGap(Assignment Assignment, int Minutes, GapReason Reason);

public sealed record StudyPlan(ImmutableArray<AssignmentProgress> Assignments,
    ImmutableArray<StudyDay> Days, ImmutableArray<PlanGap> Gaps);

public static class Scheduler
{
    public const int HorizonDays = 14;

    public static StudyPlan Build(Workspace workspace, DateOnly today)
    {
        var loggedByAssignment = workspace.Sessions
            .GroupBy(session => session.AssignmentId)
            .ToDictionary(group => group.Key, group => group.Sum(session => session.Minutes));
        var loggedByDay = workspace.Sessions
            .GroupBy(session => session.Date)
            .ToDictionary(group => group.Key, group => group.Sum(session => session.Minutes));

        var assignments = workspace.Assignments
            .OrderBy(assignment => assignment.Due)
            .ThenByDescending(assignment => assignment.Priority)
            .ThenBy(assignment => assignment.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(assignment => assignment.Id)
            .Select(assignment => new AssignmentProgress(assignment, loggedByAssignment.GetValueOrDefault(assignment.Id)))
            .ToImmutableArray();

        var upcoming = assignments.Where(item => !item.IsComplete && item.Assignment.Due >= today).ToArray();
        var remaining = upcoming.Select(item => item.Remaining).ToArray();
        var days = ImmutableArray.CreateBuilder<StudyDay>(HorizonDays);

        for (var offset = 0; offset < HorizonDays; offset++)
        {
            var date = today.AddDays(offset);
            var enabled = workspace.IncludeWeekends || date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
            var budget = enabled ? workspace.DailyMinutes : 0;
            var logged = loggedByDay.GetValueOrDefault(date);
            var available = Math.Max(0, budget - logged);
            var blocks = ImmutableArray.CreateBuilder<StudyBlock>();

            for (var index = 0; index < upcoming.Length && available > 0; index++)
            {
                var assignment = upcoming[index].Assignment;
                if (assignment.Due < date || remaining[index] == 0)
                    continue;

                var minutes = Math.Min(available, remaining[index]);
                blocks.Add(new(assignment, minutes));
                remaining[index] -= minutes;
                available -= minutes;
            }

            days.Add(new(date, enabled, budget, logged, blocks.ToImmutable()));
        }

        var lastDate = days[^1].Date;
        var gaps = assignments.Where(item => !item.IsComplete && item.Assignment.Due < today)
            .Select(item => new PlanGap(item.Assignment, item.Remaining, GapReason.Overdue))
            .Concat(upcoming.Select((item, index) => new PlanGap(item.Assignment, remaining[index],
                    item.Assignment.Due > lastDate ? GapReason.BeyondWindow : GapReason.DeadlineCapacity))
                .Where(gap => gap.Minutes > 0))
            .ToImmutableArray();

        return new(assignments, days.MoveToImmutable(), gaps);
    }
}
