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

const QUERY = `
customEvents
| where name == "Download"
| extend arch = tostring(customDimensions["arch"]), country = tostring(customDimensions["country"])
| summarize count() by day = bin(timestamp, 1d), arch, country
| order by day desc
`;

function escapeHtml(s: string): string {
  return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

function renderPage(rows: { day: Date; arch: string; country: string; count: number }[]): string {
  const total = rows.reduce((sum, r) => sum + r.count, 0);

  const byDay = new Map<string, number>();
  const byArch = new Map<string, number>();
  const byCountry = new Map<string, number>();
  for (const r of rows) {
    const dayKey = r.day.toISOString().slice(0, 10);
    byDay.set(dayKey, (byDay.get(dayKey) ?? 0) + r.count);
    byArch.set(r.arch, (byArch.get(r.arch) ?? 0) + r.count);
    byCountry.set(r.country, (byCountry.get(r.country) ?? 0) + r.count);
  }

  const tableRows = (m: Map<string, number>) =>
    [...m.entries()]
      .sort((a, b) => b[1] - a[1])
      .map(([k, v]) => `<tr><td>${escapeHtml(k)}</td><td>${v}</td></tr>`)
      .join("\n");

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
</style>
</head>
<body>
<h1>Accentra for Mac — download activity</h1>
<div class="total">${total} total downloads</div>

<h2>By day</h2>
<table><tr><th>Day</th><th>Downloads</th></tr>${tableRows(byDay)}</table>

<h2>By architecture</h2>
<table><tr><th>Arch</th><th>Downloads</th></tr>${tableRows(byArch)}</table>

<h2>By country</h2>
<table><tr><th>Country</th><th>Downloads</th></tr>${tableRows(byCountry)}</table>
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
  const rows = table.rows.map((row) => ({
    day: new Date(row[table.columnDescriptors.findIndex((c) => c.name === "day")] as string),
    arch: row[table.columnDescriptors.findIndex((c) => c.name === "arch")] as string,
    country: row[table.columnDescriptors.findIndex((c) => c.name === "country")] as string,
    count: row[table.columnDescriptors.findIndex((c) => c.name === "count_")] as number,
  }));

  return {
    status: 200,
    headers: { "Content-Type": "text/html; charset=utf-8" },
    body: renderPage(rows),
  };
}

app.http("stats", {
  methods: ["GET"],
  authLevel: "anonymous",
  route: "stats",
  handler: stats,
});
