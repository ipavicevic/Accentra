import {
  app,
  HttpRequest,
  HttpResponseInit,
  InvocationContext,
} from "@azure/functions";
import * as appInsights from "applicationinsights";
import * as geoip from "geoip-lite";

const REPO = "ipavicevic/Accentra";
const LATEST_RELEASE_URL = `https://api.github.com/repos/${REPO}/releases/latest`;
const CACHE_TTL_MS = 5 * 60 * 1000; // 5 minutes

// Own-machine testing shouldn't pollute real activity counts. Comma-separated
// list of public IPs (v4 and/or v6) to redirect normally but never log.
// Deliberately not committed with a real value (this repo is public) --
// passed in at deploy time as an app setting instead.
const EXCLUDED_IPS = new Set(
  (process.env.EXCLUDED_IPS ?? "")
    .split(",")
    .map((ip) => ip.trim())
    .filter(Boolean),
);

type Arch = "arm64" | "x64";

interface LatestReleaseCache {
  fetchedAt: number;
  assetUrls: Partial<Record<Arch, string>>;
}

let cache: LatestReleaseCache | null = null;

// Resolves the current latest release's DMG asset URLs, cached briefly so
// docs/mac.html's links never need updating on a new release without
// hammering the GitHub API on every download.
async function getLatestAssetUrls(): Promise<Partial<Record<Arch, string>>> {
  if (cache && Date.now() - cache.fetchedAt < CACHE_TTL_MS) {
    return cache.assetUrls;
  }

  const res = await fetch(LATEST_RELEASE_URL, {
    headers: { Accept: "application/vnd.github+json", "User-Agent": "accentra-download-api" },
  });
  if (!res.ok) {
    throw new Error(`GitHub API returned ${res.status} for latest release`);
  }
  const release = (await res.json()) as { assets: { name: string; browser_download_url: string }[] };

  const assetUrls: Partial<Record<Arch, string>> = {};
  for (const asset of release.assets) {
    if (asset.name.endsWith(`-arm64.dmg`)) assetUrls.arm64 = asset.browser_download_url;
    else if (asset.name.endsWith(`-x64.dmg`)) assetUrls.x64 = asset.browser_download_url;
  }

  cache = { fetchedAt: Date.now(), assetUrls };
  return assetUrls;
}

// Extracts the client IP from X-Forwarded-For, correctly handling both IPv4
// ("1.2.3.4:5678") and IPv6 -- which contains colons as part of the address
// itself, so a naive split(":")[0] truncates it (e.g. down to just "2601").
// IPv6 with a port is bracketed ("[2601:600::1]:5678" per standard
// disambiguation); bare IPv6 has no port and multiple colons.
function extractIp(forwardedFor: string): string | undefined {
  const first = forwardedFor.split(",")[0]?.trim();
  if (!first) return undefined;
  if (first.startsWith("[")) {
    const end = first.indexOf("]");
    return end > 0 ? first.slice(1, end) : undefined;
  }
  if ((first.match(/:/g) ?? []).length > 1) {
    return first; // bare IPv6, no port
  }
  return first.split(":")[0]; // IPv4, possibly with :port
}

export async function downloadRedirect(
  request: HttpRequest,
  context: InvocationContext,
): Promise<HttpResponseInit> {
  const arch = request.params.arch as Arch;
  if (arch !== "arm64" && arch !== "x64") {
    return { status: 400, body: "arch must be arm64 or x64" };
  }

  let assetUrls: Partial<Record<Arch, string>>;
  try {
    assetUrls = await getLatestAssetUrls();
  } catch (err) {
    context.error("Failed to resolve latest release", err);
    return { status: 502, body: "Could not resolve the latest release" };
  }

  const url = assetUrls[arch];
  if (!url) {
    return { status: 404, body: `No ${arch} asset found on the latest release` };
  }

  // Resolve country locally, in-process (geoip-lite bundles its own MaxMind
  // GeoLite2-derived database -- no external API call, no third party ever
  // sees the IP). The IP itself is never stored or transmitted anywhere;
  // only the resulting country -- and, transiently, the exclusion check
  // below -- ever touch it.
  const ip = extractIp(request.headers.get("x-forwarded-for") ?? "");
  const country = ip ? (geoip.lookup(ip)?.country ?? "unknown") : "unknown";

  if (!ip || !EXCLUDED_IPS.has(ip)) {
    appInsights.defaultClient?.trackEvent({
      name: "Download",
      properties: { arch, country },
    });
  }

  return { status: 302, headers: { Location: url } };
}

app.http("downloadRedirect", {
  methods: ["GET"],
  authLevel: "anonymous",
  route: "mac/{arch}",
  handler: downloadRedirect,
});
