using StudyFlow.Core;

namespace StudyFlow.Tests;

public sealed class SchedulerTests
{
    private static readonly DateOnly Monday = new(2026, 11, 16);

    [Fact]
    public void SchedulingOrderIsDeadlineThenPriority()
    {
        var workspace = Add(Workspace.Empty, "Later high", Monday.AddDays(1), 40, Priority.High);
        workspace = Add(workspace, "Due today low", Monday, 30, Priority.Low);
        workspace = Add(workspace, "Due today high", Monday, 40, Priority.High);
        workspace = WorkspaceRules.ChangeBudget(workspace, 50, true);

        var plan = Scheduler.Build(workspace, Monday);

        Assert.Collection(plan.Days[0].Blocks,
            block => Assert.Equal(("Due today high", 40), (block.Assignment.Title, block.Minutes)),
            block => Assert.Equal(("Due today low", 10), (block.Assignment.Title, block.Minutes)));
        Assert.Equal("Later high", Assert.Single(plan.Days[1].Blocks).Assignment.Title);
        var gap = Assert.Single(plan.Gaps);
        Assert.Equal(("Due today low", 20, GapReason.DeadlineCapacity),
            (gap.Assignment.Title, gap.Minutes, gap.Reason));
    }

    [Fact]
    public void LoggedTimeReducesBothWorkAndTodaysAvailableBudget()
    {
        var workspace = Add(Workspace.Empty, "Practice", Monday.AddDays(1), 125);
        workspace = WorkspaceRules.Log(workspace, workspace.Assignments[0].Id, Monday, 35, Monday);

        var plan = Scheduler.Build(workspace, Monday);

        Assert.Equal(90, Assert.Single(plan.Assignments).Remaining);
        Assert.Equal(35, plan.Days[0].Logged);
        Assert.Equal(55, plan.Days[0].Planned);
        Assert.Equal(35, plan.Days[1].Planned);
        Assert.Empty(plan.Gaps);
    }

    [Fact]
    public void WeekendBlocksAreSkippedAndMissedDeadlinesStayVisible()
    {
        var friday = Monday.AddDays(-3);
        var workspace = Add(Workspace.Empty, "Already late", friday.AddDays(-1), 30);
        workspace = Add(workspace, "Sunday deadline", friday.AddDays(2), 150);
        workspace = WorkspaceRules.ChangeBudget(workspace, 90, false);

        var plan = Scheduler.Build(workspace, friday);

        Assert.Equal(90, plan.Days[0].Planned);
        Assert.All(plan.Days.Skip(1).Take(2), day =>
        {
            Assert.False(day.Enabled);
            Assert.Empty(day.Blocks);
        });
        Assert.Empty(plan.Days[3].Blocks);
        Assert.Collection(plan.Gaps,
            gap => Assert.Equal((30, GapReason.Overdue), (gap.Minutes, gap.Reason)),
            gap => Assert.Equal((60, GapReason.DeadlineCapacity), (gap.Minutes, gap.Reason)));
    }

    [Fact]
    public void FartherDeadlinesCarryWorkPastTheWindow()
    {
        var workspace = Add(Workspace.Empty, "Research", Monday.AddDays(20), 240);
        workspace = WorkspaceRules.ChangeBudget(workspace, 15, true);

        var plan = Scheduler.Build(workspace, Monday);

        Assert.Equal(Scheduler.HorizonDays, plan.Days.Length);
        Assert.Equal(Monday.AddDays(13), plan.Days[^1].Date);
        Assert.Equal(210, plan.Days.Sum(day => day.Planned));
        var gap = Assert.Single(plan.Gaps);
        Assert.Equal((30, GapReason.BeyondWindow), (gap.Minutes, gap.Reason));
    }

    [Fact]
    public void FullWorkspaceKeepsCapacityAndPerAssignmentTotalsConsistent()
    {
        var workspace = Workspace.Empty;
        for (var index = 0; index < WorkspaceRules.MaxAssignments; index++)
            workspace = Add(workspace, $"Task {index}", Monday.AddDays(index % 18 - 2), 15 + index % 100);

        var plan = Scheduler.Build(workspace, Monday);
        Assert.All(plan.Days, day =>
        {
            Assert.InRange(day.Planned, 0, day.Budget);
            Assert.All(day.Blocks, block => Assert.True(day.Date <= block.Assignment.Due));
        });

        var blocks = plan.Days.SelectMany(day => day.Blocks).ToLookup(block => block.Assignment.Id);
        var gaps = plan.Gaps.ToDictionary(gap => gap.Assignment.Id, gap => gap.Minutes);
        Assert.All(plan.Assignments, assignment => Assert.Equal(assignment.Remaining,
            blocks[assignment.Assignment.Id].Sum(block => block.Minutes) + gaps.GetValueOrDefault(assignment.Assignment.Id)));
    }

    [Fact]
    public void CompletedAssignmentsAndDaysWithSpentBudgetsNeedNoMoreBlocks()
    {
        var workspace = Add(Workspace.Empty, "Done", Monday, 120);
        workspace = Add(workspace, "Next", Monday.AddDays(1), 30);
        workspace = WorkspaceRules.Log(workspace, workspace.Assignments[0].Id, Monday, 120, Monday);
        var plan = Scheduler.Build(workspace, Monday);

        Assert.True(plan.Assignments[0].IsComplete);
        Assert.Empty(plan.Days[0].Blocks);
        Assert.Equal("Next", Assert.Single(plan.Days[1].Blocks).Assignment.Title);
        Assert.Empty(plan.Gaps);
    }

    private static Workspace Add(Workspace workspace, string title, DateOnly due, int minutes,
        Priority priority = Priority.Normal) =>
        WorkspaceRules.SaveAssignment(workspace, null, title, "Math", due, minutes, priority);
}
