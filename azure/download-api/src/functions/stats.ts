import {
  app,
  HttpRequest,
  HttpResponseInit,
  InvocationContext,
} from "@azure/functions";
import { DefaultAzureCredential } from "@azure/identity";
import { LogsQueryClient, LogsQueryResultStatus } from "@azure/monitor-query";

// Use DefaultAzureCredential - works with:
// - Managed Identity (in Azure)
// - Azure CLI credentials (local development)
const credential = new DefaultAzureCredential();
const client = new LogsQueryClient(credential);

const APP_INSIGHTS_RESOURCE_ID = process.env.APPLICATIONINSIGHTS_RESOURCE_ID || "";

// Raw per-event rows, not pre-aggregated by day -- Application Insights
// timestamps are UTC, and only the browser knows the viewer's local
// timezone, so day-bucketing has to happen client-side to be correct.
const QUERY = `
customEvents
| where name == "Download"
| extend arch = tostring(customDimensions["arch"]), country = tostring(customDimensions["country"])
| project timestamp, arch, country
| order by timestamp desc
`;

interface DownloadEvent {
  timestamp: string;
  arch: string;
  country: string;
}

function renderPage(events: DownloadEvent[]): string {
  // Data volume is tiny (dozens of events) -- embedding it inline and doing
  // all aggregation in the browser is simpler than a separate JSON endpoint,
  // and it's the only way to bucket "by day" using the viewer's own
  // timezone rather than Application Insights' UTC timestamps.
  const eventsJson = JSON.stringify(events).replace(/</g, "\\u003c");

  return `<!doctype html>
<html>
<head>
<meta charset="utf-8">
<title>Accentra for Mac — download activity</title>
<style>
  body { font-family: -apple-system, sans-serif; max-width: 720px; margin: 40px auto; padding: 0 16px; }
  h1 { font-size: 1.4rem; }
  .total { font-size: 2rem; font-weight: 700; margin: 8px 0 24px; }
  table { border-collapse: collapse; width: 100%; margin-bottom: 32px; }
  th, td { text-align: left; padding: 6px 12px; border-bottom: 1px solid #ddd; }
  th { color: #666; font-weight: 600; font-size: 0.85rem; }
  .tz-note { color: #888; font-size: 0.8rem; margin-top: -16px; margin-bottom: 24px; }
</style>
</head>
<body>
<h1>Accentra for Mac — download activity</h1>
<div class="total" id="total"></div>

<h2>By day</h2>
<p class="tz-note" id="tz-note"></p>
<table id="by-day"><tr><th>Day</th><th>Downloads</th></tr></table>

<h2>By architecture</h2>
<table id="by-arch"><tr><th>Arch</th><th>Downloads</th></tr></table>

<h2>By country</h2>
<table id="by-country"><tr><th>Country</th><th>Downloads</th></tr></table>

<script>
const events = ${eventsJson};

function escapeHtml(s) {
  return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

function countBy(keyFn) {
  const m = new Map();
  for (const e of events) {
    const k = keyFn(e);
    m.set(k, (m.get(k) ?? 0) + 1);
  }
  return m;
}

function renderTable(id, m) {
  const rows = [...m.entries()]
    .sort((a, b) => b[1] - a[1])
    .map(([k, v]) => "<tr><td>" + escapeHtml(k) + "</td><td>" + v + "</td></tr>")
    .join("");
  document.getElementById(id).innerHTML =
    document.getElementById(id).rows[0].outerHTML + rows;
}

// Local calendar day (YYYY-MM-DD) from the viewer's own timezone, not UTC.
function localDayKey(isoTimestamp) {
  const d = new Date(isoTimestamp);
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return y + "-" + m + "-" + day;
}

document.getElementById("total").textContent = events.length + " total downloads";
document.getElementById("tz-note").textContent =
  "Shown in your browser's local timezone (" + Intl.DateTimeFormat().resolvedOptions().timeZone + ").";
renderTable("by-day", countBy((e) => localDayKey(e.timestamp)));
renderTable("by-arch", countBy((e) => e.arch));
renderTable("by-country", countBy((e) => e.country));
</script>
</body>
</html>`;
}

export async function stats(
  request: HttpRequest,
  context: InvocationContext,
): Promise<HttpResponseInit> {
  if (!APP_INSIGHTS_RESOURCE_ID) {
    return { status: 500, body: "APPLICATIONINSIGHTS_RESOURCE_ID is not configured" };
  }

  const result = await client.queryResource(APP_INSIGHTS_RESOURCE_ID, QUERY, { duration: "P90D" });

  if (result.status !== LogsQueryResultStatus.Success) {
    context.error("Log query failed", result.partialError);
    return { status: 502, body: "Query failed" };
  }

  const table = result.tables[0];
  const timestampIdx = table.columnDescriptors.findIndex((c) => c.name === "timestamp");
  const archIdx = table.columnDescriptors.findIndex((c) => c.name === "arch");
  const countryIdx = table.columnDescriptors.findIndex((c) => c.name === "country");

  const events: DownloadEvent[] = table.rows.map((row) => ({
    timestamp: row[timestampIdx] as string,
    arch: row[archIdx] as string,
    country: row[countryIdx] as string,
  }));

  return {
    status: 200,
    headers: { "Content-Type": "text/html; charset=utf-8" },
    body: renderPage(events),
  };
}

app.http("stats", {
  methods: ["GET"],
  authLevel: "anonymous",
  route: "stats",
  handler: stats,
});
