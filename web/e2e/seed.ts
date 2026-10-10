import { expect, type APIRequestContext } from "@playwright/test";

import { owner } from "./stack";

/**
 * Test data made through the API, the way the Owner's screens would make it, so the browser
 * part of a test is only the flow it is about.
 */

export const staff = {
  userName: "sara1405",
  fullName: "سارا میزبان",
  temporaryPassword: "temporary desk password 1405",
  password: "front desk lamp river 1405",
};

export const member = {
  fullName: "رضا احمدی",
  phoneNumber: "09121234567",
  birthDate: "1990-06-15",
};

interface Session {
  accessToken: string;
  mustChangePassword: boolean;
}

async function login(api: APIRequestContext, userName: string, password: string) {
  const response = await api.post("/api/auth/login", { data: { userName, password } });
  expect(response.status(), `login as ${userName}`).toBe(200);
  return (await response.json()) as Session;
}

async function changePassword(
  api: APIRequestContext,
  token: string,
  current: string,
  next: string,
) {
  const response = await api.post("/api/auth/change-password", {
    headers: { Authorization: `Bearer ${token}` },
    data: { currentPassword: current, newPassword: next },
  });
  expect(response.status(), "change password").toBe(200);
  return ((await response.json()) as Session).accessToken;
}

async function send(
  api: APIRequestContext,
  token: string,
  method: "POST" | "PUT",
  path: string,
  data: unknown,
) {
  const response = await api.fetch(path, {
    method,
    headers: { Authorization: `Bearer ${token}` },
    data,
  });
  expect(response.ok(), `${method} ${path}: ${response.status()} ${await response.text()}`).toBe(
    true,
  );
  return response.status() === 204
    ? null
    : ((await response.json()) as { id: string; version?: number });
}

/**
 * The Owner's first login (the seeded password must be changed), the gym's prices, a staff
 * account past its own first login, and a member holding a 10-session plan.
 */
export async function seedFrontDesk(api: APIRequestContext) {
  const first = await login(api, owner.userName, owner.password);
  const ownerToken = await changePassword(
    api,
    first.accessToken,
    owner.password,
    `${owner.password} changed`,
  );

  const pricesResponse = await api.get("/api/pricing", {
    headers: { Authorization: `Bearer ${ownerToken}` },
  });
  const { version } = (await pricesResponse.json()) as { version: number };
  await send(api, ownerToken, "PUT", "/api/pricing", {
    sessionPrice: 90_000,
    singleVisitPrice: 150_000,
    version,
  });

  await send(api, ownerToken, "POST", "/api/staff", {
    userName: staff.userName,
    fullName: staff.fullName,
    temporaryPassword: staff.temporaryPassword,
  });
  const staffFirst = await login(api, staff.userName, staff.temporaryPassword);
  await changePassword(api, staffFirst.accessToken, staff.temporaryPassword, staff.password);

  const created = await send(api, ownerToken, "POST", "/api/members", {
    fullName: member.fullName,
    phoneNumber: member.phoneNumber,
    notes: null,
    birthDate: member.birthDate,
  });
  await send(api, ownerToken, "POST", `/api/members/${created!.id}/subscriptions`, {
    sessionCount: 10,
  });
}
