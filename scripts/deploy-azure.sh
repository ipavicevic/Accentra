#!/usr/bin/env bash
# Deploy Azure resources (Bicep) and the download-api Function code
set -euo pipefail

RESOURCE_GROUP="Accentra-Mac"
FUNCTION_APP="accentra-mac-dl"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

# ACCENTRA_EXCLUDED_IPS: comma-separated public IPs to exclude from download
# activity logging (e.g. the maintainer's own machine, for testing). Set this
# in your own shell profile -- never committed, this repo is public.
echo "=== [1/2] Deploying Azure resources ==="
az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$REPO_ROOT/infra/main.bicep" \
  --parameters excludedIps="${ACCENTRA_EXCLUDED_IPS:-}" \
  --output table

echo ""
echo "=== [2/2] Deploying download-api code ==="
# Bicep resets WEBSITE_RUN_FROM_PACKAGE which can wipe wwwroot -- always redeploy code after Bicep
cd "$REPO_ROOT/azure/download-api"
npm install
npm run build
npm prune --omit=dev
func azure functionapp publish "$FUNCTION_APP" --no-build
