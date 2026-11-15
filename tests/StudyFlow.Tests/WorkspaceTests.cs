using StudyFlow.Core;

namespace StudyFlow.Tests;

public sealed class WorkspaceTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);

    [Fact]
    public void EditPreservesIdentityAndDeletingAssignmentRemovesItsHistory()
    {
        var original = Add();
        var id = original.Assignments[0].Id;
        var logged = WorkspaceRules.Log(original, id, Today, 20, Today);
        var edited = WorkspaceRules.SaveAssignment(logged, id, " New title ", " Math ", Today, 35, Priority.High);
        Assert.Empty(original.Sessions);
        Assert.Equal(id, edited.Assignments[0].Id);
        Assert.Equal("New title", edited.Assignments[0].Title);
        Assert.Equal("Math", edited.Assignments[0].Subject);
        Assert.Equal(20, Assert.Single(edited.Sessions).Minutes);
        var deleted = WorkspaceRules.DeleteAssignment(edited, id);
        Assert.Empty(deleted.Assignments);
        Assert.Empty(deleted.Sessions);
    }

    [Fact]
    public void LoggingRejectsInvalidDataAndRemovingTimeRestoresWorkload()
    {
        var workspace = Add();
        var id = workspace.Assignments[0].Id;
        Assert.Throws<PlannerRuleException>(() => WorkspaceRules.Log(workspace, id, Today.AddDays(1), 20, Today));
        Assert.Throws<PlannerRuleException>(() => WorkspaceRules.Log(workspace, id, Today, 46, Today));
        Assert.Throws<PlannerRuleException>(() => WorkspaceRules.Log(workspace, id, Today, 0, Today));
        var logged = WorkspaceRules.Log(workspace, id, Today, 45, Today);
        Assert.Throws<PlannerRuleException>(() => WorkspaceRules.SaveAssignment(logged, id, "Plan", "Math", Today, 30, Priority.Normal));
        var removed = WorkspaceRules.DeleteSession(logged, logged.Sessions[0].Id);
        Assert.Equal(45, Scheduler.Build(removed, Today).Assignments[0].Remaining);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(481)]
    public void RejectsBudgetOutsideRange(int minutes) =>
        Assert.Throws<PlannerRuleException>(() => WorkspaceRules.ChangeBudget(Workspace.Empty, minutes, false));

    [Theory]
    [InlineData("", "Math", 15)]
    [InlineData("Title", "  ", 15)]
    [InlineData("Title", "Math", 14)]
    [InlineData("Title", "Math", 2401)]
    public void AssignmentInputsHaveBounds(string title, string subject, int estimate) =>
        Assert.Throws<PlannerRuleException>(() => WorkspaceRules.SaveAssignment(Workspace.Empty, null, title,
            subject, Today, estimate, Priority.Normal));

    [Fact]
    public void JsonRoundTripPreservesPlanData()
    {
        var workspace = Add();
        workspace = WorkspaceRules.Log(workspace, workspace.Assignments[0].Id, Today, 20, Today);
        workspace = WorkspaceRules.ChangeBudget(workspace, 120, false);

        var restored = WorkspaceJson.Read(WorkspaceJson.Write(workspace));
        Assert.Equal(workspace.DailyMinutes, restored.DailyMinutes);
        Assert.Equal(workspace.IncludeWeekends, restored.IncludeWeekends);
        Assert.Equal(workspace.Assignments.AsEnumerable(), restored.Assignments.AsEnumerable());
        Assert.Equal(workspace.Sessions.AsEnumerable(), restored.Sessions.AsEnumerable());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"version\":2,\"dailyMinutes\":90,\"includeWeekends\":true,\"assignments\":[],\"sessions\":[]}")]
    [InlineData("{\"version\":1,\"dailyMinutes\":90,\"includeWeekends\":true,\"assignments\":[null],\"sessions\":[]}")]
    public void RejectsInvalidBackups(string json) =>
        Assert.Throws<PlannerRuleException>(() => WorkspaceJson.Read(json));

    [Fact]
    public void RejectsOrphanSessionsAndDuplicateAssignmentIds()
    {
        var workspace = Add();
        var orphan = new StudySession(Guid.NewGuid(), Guid.NewGuid(), Today, 10);
        Assert.Throws<PlannerRuleException>(() => WorkspaceJson.Read(WorkspaceJson.Write(workspace with { Sessions = [orphan] })));
        Assert.Throws<PlannerRuleException>(() => WorkspaceJson.Read(WorkspaceJson.Write(workspace with
        {
            Assignments = [workspace.Assignments[0], workspace.Assignments[0]]
        })));
    }

    private static Workspace Add() =>
        WorkspaceRules.SaveAssignment(Workspace.Empty, null, "Plan", "Math", Today, 45, Priority.Normal);
}
