# StudyFlow

**Madiyar Makhan · IT-2508**

Introduction to Functional Programming final project

Enter your assignments, deadlines, and daily study budget. StudyFlow plans the next two weeks and shows which work won’t fit. When you log a study session, the plan updates.

## Run locally

You’ll need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```sh
dotnet restore StudyFlow.slnx
dotnet run --project src/StudyFlow --launch-profile http --urls http://127.0.0.1:5078
```

Open [StudyFlow](http://127.0.0.1:5078). Start with your own assignments or load the example from the dashboard.

## Use the planner

- Add an assignment with a subject, deadline, time estimate, and priority. Find it under **Assignments** to edit or delete it. Deleting it also removes its study sessions.
- Open **Schedule** to see what fits and why some work is still unscheduled. In **Settings**, choose a daily budget of 15–480 minutes and decide whether to study on weekends.
- Use **Log time** to record study. An assignment is complete when you’ve logged its full estimate. Remove a session from **Study history** to add those minutes back to the remaining work.
- From **Settings**, download a JSON backup or restore one. A restore replaces the current plan after you confirm.

Your plan is saved to this browser profile at the address and port you use. To move it to another device, take a backup. Reload the page if you leave it open overnight or another tab saves changes. If the saved plan won’t open, download a recovery copy before resetting it.

## How the planner works

It schedules unfinished assignments in deadline order, with higher priority first when deadlines match. Study already logged uses part of that day’s budget. Time that doesn’t fit is listed as overdue, over budget before the deadline, or outside the two-week window.

- `StudyFlow.Core` contains immutable workspace records, validation, JSON serialization, and the scheduler. The same workspace and start date always produce the same plan.
- `StudyFlow` contains the Razor components and `PlannerState`, scoped to each Blazor connection. Components pass user actions through callbacks. The view updates after the browser accepts a save.
- Browser storage holds one versioned JSON snapshot for each address. The app checks for edits from another tab before saving and uses Web Locks to serialize writes where supported.

A workspace holds up to 200 assignments and 2,000 study sessions. The planner groups the sessions, sorts the assignments, and checks eligible work for each of the next 14 days.

Build a release with `dotnet publish src/StudyFlow -c Release`. For deployment, keep HTTPS and WebSocket forwarding enabled. The app uses a live Blazor connection and browser storage. `GET /health` returns `200 Healthy` when the server responds.

- [Tests and rubric checklist](docs/verification.md)
- [Defense slides and speaker notes](docs/StudyFlow-Defense.pptx)
- [First push to GitHub](docs/publish.md)
- [Blazor render modes on Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0)
