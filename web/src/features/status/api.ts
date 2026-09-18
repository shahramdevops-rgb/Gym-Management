import { useQuery } from "@tanstack/react-query";

/** The values ASP.NET Core's health check writes as plain text. */
export type HealthStatus = "Healthy" | "Degraded" | "Unhealthy";

export interface HealthReport {
  status: HealthStatus;
  /** A UTC timestamp, so the page can say when it last heard from the server. */
  checkedAt: string;
}

const healthStatuses: readonly string[] = ["Healthy", "Degraded", "Unhealthy"];

/**
 * Reads /health. It is a health-check endpoint rather than an API endpoint, so it is not in the
 * OpenAPI document and uses plain fetch instead of the generated client.
 *
 * An unhealthy server answers 503 with the body "Unhealthy": that is a report, not a failure.
 * Only a network error or an unexpected body counts as "could not reach the server".
 */
export async function fetchHealth(signal?: AbortSignal): Promise<HealthReport> {
  const response = await fetch("/health", { signal, headers: { Accept: "text/plain" } });
  const body = (await response.text()).trim();

  if (!healthStatuses.includes(body)) {
    throw new Error(`Unexpected /health response: ${response.status}`);
  }

  return { status: body as HealthStatus, checkedAt: new Date().toISOString() };
}

export function useHealth() {
  return useQuery({
    queryKey: ["health"],
    queryFn: ({ signal }) => fetchHealth(signal),
    refetchInterval: 30_000,
  });
}
