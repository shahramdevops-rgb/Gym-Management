import { errorMessage, errorMessages, networkErrorMessage } from "@/lib/errors";
import { json, mockApi } from "@/test/mockApi";

import { api } from "./client";

describe("api client", () => {
  it("Client_FailureWithNoBody_ComesBackAsAnErrorWithItsStatus", async () => {
    // An API started before this route existed answers 405 with nothing in the body.
    mockApi({ "GET /api/cafe/orders": () => new Response(null, { status: 405 }) });

    const { data, error } = await api.GET("/api/cafe/orders");

    expect(data).toBeUndefined();
    expect(error).toMatchObject({ status: 405 });
    // The server answered, so the desk must not be told it could not be reached.
    expect(errorMessage(error)).not.toBe(networkErrorMessage);
    expect(errorMessage(error)).toBe(errorMessages["General.Unexpected"]);
  });

  it("Client_FailureWithAProblemBody_IsLeftAsTheApiSentIt", async () => {
    mockApi({
      "GET /api/cafe/orders": () => json(400, { status: 400, code: "CafeOrders.InvalidDateRange" }),
    });

    const { error } = await api.GET("/api/cafe/orders");

    expect(error).toMatchObject({ code: "CafeOrders.InvalidDateRange" });
  });
});
