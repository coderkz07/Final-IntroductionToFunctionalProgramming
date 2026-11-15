# Tests and rubric checklist

## Run the tests

From the project folder, run the .NET suite.

```sh
dotnet test StudyFlow.slnx -c Release
```

For browser tests, start StudyFlow in one terminal.

```sh
dotnet run --project src/StudyFlow -c Release --launch-profile http --urls http://127.0.0.1:5078
```

In a second terminal, use Node.js 22 or later.

```sh
cd tests/browser
npm ci
npx playwright install chromium firefox webkit
npm test
```

The default engine is Chromium. Set `STUDYFLOW_BROWSER` to `firefox` or `webkit` to run the same tests there. Use `STUDYFLOW_BROWSER_PATH` to point to an installed browser. Playwright may need additional system packages on Linux.

For the static Pages build, set `STUDYFLOW_STATIC=1` and set `STUDYFLOW_URL` to its base URL. The same browser suite checks direct links and planner interactions.

Each browser test starts with an empty profile and uses November 16, 2026, as the local date so the deadline and weekend checks repeat consistently.

## What the tests cover

| Area | Checks |
| --- | --- |
| Scheduling | Blocks stay within daily budgets and deadlines. Time that doesn’t fit is reported. Tests cover priority ties, weekends, an exhausted budget, and a full workspace. |
| Assignment and study history | Edit or delete assignments, log or remove sessions, and reject invalid dates and minutes. |
| Storage | Save and reload a plan, reject an invalid backup, avoid overwriting a newer save, and recover from unreadable stored data. |
| Browser | Check routes and static files, create and edit assignments, search and filter, change the budget, and export and restore a backup. |
| Keyboard and layout | Dismiss a dialog with Escape, return focus to its opener, and fit the content at desktop and phone widths. |

To check by hand, load the example plan and try the three pages at desktop and phone widths. Tab through the controls, close a dialog with Escape, and check how long assignment titles wrap.

## Rubric checklist

| Criterion | Evidence |
| --- | --- |
| 1. Code structure | Razor views, state service, browser storage bridge, and scheduling logic have separate roles. |
| 2. Reuse | Assignment lists and dialogs share typed components and event callbacks. |
| 3. Cross-platform | .NET 10, browser storage, and browser tests across three engines on Windows. |
| 4. State | Each Blazor connection has one state container. Views use the same immutable snapshot. |
| 5. UI | Shared type, spacing, and styles across the dashboard, assignments, and schedule. |
| 6. UX | Labeled inputs, skip navigation, dialog focus, and confirmation before deletion. |
| 7. Responsive layout | Breakpoints reflow the navigation and schedule. Tests check for horizontal overflow. |
| 8. Core features | Assignments, search, filters, study history, daily budget, and a two-week schedule. |
| 9. Advanced features | Deadline-aware planning, backup and restore, and save checks across tabs. |
| 10. Error handling | Input limits, backup validation, storage messages, and recovery options. |
| 11. Efficiency | One grouping of sessions, one sort of assignments, and a 14-day schedule. |
| 12. Scalability | Each connection has its own state. Workspaces hold up to 200 assignments and 2,000 sessions. |
| 13. Resource use | Browser storage, local assets, and a bounded workspace. |
| 14. Testing | Automated tests for scheduling rules, storage, HTTP responses, and browser workflows. |
| 15. QA | Keyboard and layout checks, saved state checks, and three browser engines. |
| 16. Documentation | Setup, user guide, architecture, health endpoint, and test commands. |
| 17. Innovation | Separates missed deadlines from work that needs more time or falls outside the planning window. |
| 18. Blazor and C# | Razor parameters, callbacks, dependency injection, immutable records and arrays, JS interop, and LINQ. |
| 19. Readiness | Release publish, a health endpoint, deployment notes, and example data. |
| 20. Presentation | Editable slides with wireframes, a technology comparison, architecture, a worked example, and speaker notes. |

## Verified results

- The Release build and publish succeeded with zero build warnings or errors.
- All 24 .NET tests passed. The core reached 86.3% line coverage and 92% branch coverage.
- All eight browser tests passed in each of Chrome, Firefox, and WebKit on Windows. Layout checks covered 1440, 390, and 320 px.
- The axe scan passed its selected WCAG 2.0 and 2.1 A/AA rules on the three main views and two dialogs. The desktop and phone-width dashboard layouts were visually checked.

Native macOS, Linux, and physical-device checks remain pending. The defense is a live part of the assessment.
