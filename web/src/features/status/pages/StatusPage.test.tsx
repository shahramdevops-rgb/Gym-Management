import { screen } from "@testing-library/react";

import { json, mockApi, session, staffUser } from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

// fetch is the boundary between the page and the server, so it is the one thing stubbed. The
// page sits behind the login gate, so the user is signed in and /me answers too.
function stubHealthResponse(status: number, body: string) {
  return mockApi({
    "GET /api/auth/me": () => json(200, staffUser),
    "GET /health": () => new Response(body, { status }),
  });
}

describe("StatusPage", () => {
  it("StatusPage_ServerHealthy_ShowsHealthyBadge", async () => {
    const { fetchMock } = stubHealthResponse(200, "Healthy");

    renderApp("/", { session: session() });

    expect(await screen.findByText("سالم")).toBeInTheDocument();
    expect(screen.getByText(/آخرین بررسی/)).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith("/health", expect.anything());
  });

  it("StatusPage_ServerUnhealthy_ShowsUnhealthyBadge", async () => {
    // A 503 with a known body is a health report, not a failed request.
    stubHealthResponse(503, "Unhealthy");

    renderApp("/", { session: session() });

    expect(await screen.findByText("ناسالم")).toBeInTheDocument();
    expect(screen.queryByText("سرور در دسترس نیست")).not.toBeInTheDocument();
  });

  it("StatusPage_ServerUnreachable_ShowsUnreachableBadge", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));

    renderApp("/", { session: session() });

    expect(
      await screen.findByText("سرور در دسترس نیست", {}, { timeout: 5000 }),
    ).toBeInTheDocument();
  });

  it("StatusPage_UnexpectedResponse_ShowsUnreachableBadge", async () => {
    // For example the Vite proxy's own 502 page when the API is not running.
    stubHealthResponse(502, "<html>Bad Gateway</html>");

    renderApp("/", { session: session() });

    expect(
      await screen.findByText("سرور در دسترس نیست", {}, { timeout: 5000 }),
    ).toBeInTheDocument();
  });
});
