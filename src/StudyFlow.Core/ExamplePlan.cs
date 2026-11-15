namespace StudyFlow.Core;

public static class ExamplePlan
{
    public static Workspace Build(DateOnly today)
    {
        var workspace = Workspace.Empty;
        workspace = WorkspaceRules.SaveAssignment(workspace, null, "Review lecture notes", "Computer Architecture",
            today.AddDays(1), 90, Priority.High);
        workspace = WorkspaceRules.SaveAssignment(workspace, null, "Practice recursive functions", "Functional Programming",
            today.AddDays(2), 120, Priority.High);
        workspace = WorkspaceRules.SaveAssignment(workspace, null, "Solve matrix exercises", "Linear Algebra",
            today.AddDays(4), 150, Priority.Normal);
        workspace = WorkspaceRules.SaveAssignment(workspace, null, "Read probability chapter", "Statistics",
            today.AddDays(6), 90, Priority.Low);
        workspace = WorkspaceRules.SaveAssignment(workspace, null, "Summarize logic gates", "Computer Architecture",
            today.AddDays(-1), 60, Priority.Normal);

        return workspace with
        {
            Sessions =
            [
                new(Guid.NewGuid(), workspace.Assignments[0].Id, today, 25),
                new(Guid.NewGuid(), workspace.Assignments[2].Id, today.AddDays(-1), 45),
                new(Guid.NewGuid(), workspace.Assignments[4].Id, today.AddDays(-2), 60)
            ]
        };
    }
}
