import assert from "node:assert/strict";
import { mkdir } from "node:fs/promises";
import { createRequire } from "node:module";
import { join } from "node:path";
import { after, before, test } from "node:test";

const require = createRequire(import.meta.url);
const playwright = require(process.env.STUDYFLOW_PLAYWRIGHT_PATH || "playwright");
const engine = process.env.STUDYFLOW_BROWSER || "chromium";
const base = (process.env.STUDYFLOW_URL || "http://127.0.0.1:5078").replace(/\/+$/, "");
const staticSite = process.env.STUDYFLOW_STATIC === "1";
const key = "studyflow.workspace.v1";
const screenshots = process.env.STUDYFLOW_SCREENSHOTS;
const fixedTime = new Date("2026-11-16T12:00:00Z");
let browser;

before(async () => {
    browser = await playwright[engine].launch({
        executablePath: process.env.STUDYFLOW_BROWSER_PATH || undefined
    });
});

after(async () => browser?.close());

async function withPage(run, viewport = { width: 1440, height: 900 }) {
    const context = await browser.newContext({ viewport, timezoneId: "UTC", acceptDownloads: true });
    const page = await context.newPage();
    page.setDefaultTimeout(8000);
    const errors = [];
    page.on("pageerror", error => errors.push(error.message));

    try {
        await page.clock.setFixedTime(fixedTime);
        await run(page, context);
        assert.deepEqual(errors, []);
    } finally {
        await context.close();
    }
}

async function openDashboard(page) {
    await page.goto(base + "/");
    await page.getByRole("button", { name: "New assignment" }).waitFor();
    await page.getByRole("heading", { name: "Dashboard", exact: true }).waitFor();
}

async function addAssignment(page, title, minutes = "45", date = "2026-11-17") {
    await page.getByRole("button", { name: "New assignment" }).click();
    const dialog = page.getByRole("dialog", { name: "New assignment" });
    await dialog.getByLabel("Assignment title").fill(title);
    await dialog.getByLabel("Subject").fill("Algebra");
    await dialog.getByLabel("Due date").fill(date);
    await dialog.getByLabel("Estimate (minutes)").fill(minutes);
    await dialog.getByRole("button", { name: "Save assignment" }).click();
    await dialog.waitFor({ state: "hidden" });
    const stored = await page.evaluate(storageKey => JSON.parse(localStorage.getItem(storageKey)), key);
    assert.equal(stored.assignments.find(assignment => assignment.title === title)?.due, date);
}

async function navigate(page, name) {
    await page.getByRole("navigation", { name: "Workspace" }).getByRole("link", { name }).click();
    await page.getByRole("heading", { name, exact: true }).waitFor();
}

async function capture(page, name) {
    if (!screenshots || engine !== "chromium") return;
    await mkdir(screenshots, { recursive: true });
    await page.evaluate(() => document.fonts.ready);
    await page.screenshot({ path: join(screenshots, name + ".png") });
}

test("serves direct routes, static assets, and a 404", async () => {
    await withPage(async page => {
        if (!staticSite) {
            const health = await page.request.get(base + "/health");
            assert.equal(health.status(), 200);
            assert.equal(await health.text(), "Healthy");
        }
        assert.equal((await page.request.get(base + "/app.css")).status(), 200);
        const assignments = await page.goto(base + "/assignments");
        assert.equal(assignments.status(), 200);
        await page.getByRole("heading", { name: "Assignment list" }).waitFor();
        const schedule = await page.goto(base + "/schedule");
        assert.equal(schedule.status(), 200);
        await page.getByRole("heading", { name: "Two-week plan" }).waitFor();
        assert.equal((await page.goto(base + "/missing-page")).status(), 404);
    });
});

test("creates, finds, edits, persists, and deletes an assignment", async () => {
    await withPage(async page => {
        await openDashboard(page);
        await addAssignment(page, "Matrix review", "120");
        await page.getByText("Matrix review", { exact: true }).waitFor();
        await navigate(page, "Assignments");
        await page.getByLabel("Search assignments").fill("calculus");
        await page.getByText("No assignments match these filters.").waitFor();
        await page.getByLabel("Search assignments").fill("ALGebRA");
        await page.getByText("Matrix review", { exact: true }).waitFor();
        await page.getByRole("button", { name: "Edit Matrix review" }).click();
        const edit = page.getByRole("dialog", { name: "Edit assignment" });
        await edit.getByLabel("Assignment title").fill("Matrix practice");
        await edit.getByRole("button", { name: "Save assignment" }).click();
        await page.getByText("Matrix practice", { exact: true }).waitFor();
        await page.reload();
        await page.getByRole("button", { name: "Edit Matrix practice" }).click();
        await page.getByRole("button", { name: "Delete", exact: true }).click();
        await page.getByRole("button", { name: "Delete assignment", exact: true }).click();
        await page.getByText("No assignments match these filters.").waitFor();
        await page.reload();
        await page.getByText("No assignments match these filters.").waitFor();
    });
});

test("logs time, rejects excess time, and restores workload when a session is removed", async () => {
    await withPage(async page => {
        await openDashboard(page);
        await addAssignment(page, "Practice integrals");
        await page.getByRole("button", { name: "Log time" }).click();
        const dialog = page.getByRole("dialog", { name: "Log study time" });
        await dialog.getByLabel("Study date").fill("2026-11-15");
        await dialog.getByLabel("Minutes studied").fill("46");
        await dialog.getByRole("button", { name: "Save session" }).click();
        await dialog.getByRole("alert").filter({ hasText: "45 minutes left" }).waitFor();
        await dialog.getByLabel("Minutes studied").fill("45");
        await dialog.getByRole("button", { name: "Save session" }).click();
        await dialog.waitFor({ state: "hidden" });
        await navigate(page, "Assignments");
        await page.getByLabel("Status", { exact: true }).selectOption("complete");
        await page.getByRole("button", { name: "Edit Practice integrals" }).waitFor();
        await page.getByText("Nov 15, 2026", { exact: true }).waitFor();
        await page.getByRole("button", { name: "Remove 45-minute session for Practice integrals" }).click();
        await page.getByRole("button", { name: "Remove session", exact: true }).click();
        await page.getByText("No assignments match these filters.").waitFor();
        await page.getByLabel("Status", { exact: true }).selectOption("open");
        await page.getByText("45 min left", { exact: true }).waitFor();
    });
});

test("applies the budget, explains a shortfall, and skips weekends", async () => {
    await withPage(async page => {
        await openDashboard(page);
        await addAssignment(page, "Exam problems", "120", "2026-11-16");
        await navigate(page, "Schedule");
        await page.getByText("90 min planned", { exact: true }).waitFor();
        await page.getByRole("button", { name: "Change budget" }).click();
        const settings = page.getByRole("dialog", { name: "Planning settings" });
        await settings.getByLabel("Daily budget (minutes)").fill("60");
        await settings.getByLabel("Plan sessions on weekends").uncheck();
        await settings.getByRole("button", { name: "Save settings" }).click();
        await page.getByText("60 min planned", { exact: true }).waitFor();
        await page.getByText("Not enough time before the deadline", { exact: true }).waitFor();
        assert.equal(await page.getByText("Rest day", { exact: true }).count(), 4);
        await page.reload();
        await page.getByText("60 min planned", { exact: true }).waitFor();
    });
});

test("downloads a backup, rejects invalid imports, and restores a valid snapshot", async () => {
    await withPage(async page => {
        await openDashboard(page);
        await addAssignment(page, "Keep me");
        await page.getByRole("button", { name: "Settings", exact: true }).click();
        const settings = page.getByRole("dialog", { name: "Planning settings" });
        const downloadPromise = page.waitForEvent("download");
        await settings.getByRole("button", { name: "Download backup" }).click();
        const download = await downloadPromise;
        assert.equal(download.suggestedFilename(), "studyflow-backup.json");
        const stream = await download.createReadStream();
        const chunks = [];
        for await (const chunk of stream) chunks.push(chunk);
        const backup = Buffer.concat(chunks);
        assert.equal(JSON.parse(backup).assignments[0].title, "Keep me");

        const file = settings.getByLabel("Choose a backup to restore");
        await file.setInputFiles({ name: "broken.json", mimeType: "application/json", buffer: Buffer.from("{x") });
        await settings.getByRole("alert").waitFor();
        await settings.getByRole("button", { name: "Close dialog" }).click();
        await addAssignment(page, "Temporary");
        await page.getByRole("button", { name: "Settings", exact: true }).click();
        await file.setInputFiles({ name: "backup.json", mimeType: "application/json", buffer: backup });
        await page.getByRole("button", { name: "Restore backup" }).click();
        await page.getByRole("status").filter({ hasText: "Backup restored." }).waitFor();
        assert.equal(await page.getByText("Temporary", { exact: true }).count(), 0);
        await page.getByText("Keep me", { exact: true }).waitFor();
    });
});

test("rejects a stale tab write without erasing the newer plan", async () => {
    await withPage(async (page, context) => {
        await openDashboard(page);
        const other = await context.newPage();
        await other.clock.setFixedTime(fixedTime);
        await openDashboard(other);
        await addAssignment(page, "First tab");
        await other.getByRole("button", { name: "Load example plan" }).click();
        await other.getByRole("alert").filter({ hasText: "Another tab changed the plan" }).waitFor();
        await other.reload();
        await other.getByText("First tab", { exact: true }).waitFor();
        assert.equal(await other.getByText("Review lecture notes", { exact: true }).count(), 0);
    });
});

test("recovers unreadable local data without overwriting it on load", async () => {
    await withPage(async page => {
        await page.addInitScript(storageKey => localStorage.setItem(storageKey, "{broken"), key);
        await page.goto(base + "/");
        await page.getByRole("heading", { name: "Couldn’t open your plan" }).waitFor();
        assert.equal(await page.evaluate(storageKey => localStorage.getItem(storageKey), key), "{broken");
        await page.getByRole("button", { name: "Reset storage" }).click();
        await page.getByRole("button", { name: "Reset workspace" }).click();
        await page.getByRole("button", { name: "New assignment" }).waitFor();
        assert.deepEqual(JSON.parse(await page.evaluate(storageKey => localStorage.getItem(storageKey), key)).assignments, []);
    });
});

test("supports mobile layout, keyboard dismissal, and dashboard rendering", async () => {
    await withPage(async page => {
        await openDashboard(page);
        await page.getByRole("button", { name: "Load example plan" }).click();
        await page.getByText("Review lecture notes", { exact: true }).waitFor();
        await capture(page, "dashboard");
        const button = page.getByRole("button", { name: "New assignment" });
        await button.focus();
        await page.keyboard.press("Enter");
        const dialog = page.getByRole("dialog", { name: "New assignment" });
        await dialog.waitFor();
        await page.keyboard.press("Escape");
        await dialog.waitFor({ state: "hidden" });
        assert.equal(await button.evaluate(element => element === document.activeElement), true);
        await page.setViewportSize({ width: 390, height: 844 });
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
        await capture(page, "mobile");
        await page.setViewportSize({ width: 320, height: 720 });
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
        await navigate(page, "Schedule");
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
        await page.setViewportSize({ width: 1440, height: 900 });
        await capture(page, "schedule");
    });
});
