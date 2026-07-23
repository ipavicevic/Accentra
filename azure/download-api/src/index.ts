import * as appInsights from "applicationinsights";

// The Functions host auto-collects its own request/dependency telemetry
// regardless, but this package's own defaultClient (used for our custom
// trackEvent calls) stays uninitialized -- and silently no-ops -- unless we
// explicitly start it here. Picks up APPLICATIONINSIGHTS_CONNECTION_STRING
// from the environment automatically.
appInsights.setup().start();

// Import all function registrations
import "./functions/downloadRedirect";
import "./functions/stats";
