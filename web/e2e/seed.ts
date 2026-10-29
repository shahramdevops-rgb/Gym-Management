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

/** The Owner's password after their first login, when the seeded one has to be changed. */
export const ownerPassword = `${owner.password} changed`;

/** The Owner's first login: the seeded password is changed to {@link ownerPassword}. */
export async function changeOwnerPassword(api: APIRequestContext) {
  const first = await login(api, owner.userName, owner.password);
  return changePassword(api, first.accessToken, owner.password, ownerPassword);
}

/**
 * The Owner's first login (the seeded password must be changed), the gym's prices, a staff
 * account past its own first login, and a member holding a 10-session plan.
 */
export async function seedFrontDesk(api: APIRequestContext) {
  const ownerToken = await changeOwnerPassword(api);
  await setPrices(api, ownerToken);

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

/** The prices every plan is sold at; setting them twice changes nothing. */
async function setPrices(api: APIRequestContext, token: string) {
  const pricesResponse = await api.get("/api/pricing", {
    headers: { Authorization: `Bearer ${token}` },
  });
  const { version } = (await pricesResponse.json()) as { version: number };
  await send(api, token, "PUT", "/api/pricing", {
    sessionPrice: 90_000,
    singleVisitPrice: 150_000,
    version,
  });
}

/**
 * The Owner's token whether or not an earlier spec already made the first login: the specs share
 * one stack, and any of them may run alone.
 */
async function ownerToken(api: APIRequestContext) {
  const response = await api.post("/api/auth/login", {
    data: { userName: owner.userName, password: ownerPassword },
  });
  return response.ok()
    ? ((await response.json()) as Session).accessToken
    : changeOwnerPassword(api);
}

/** A trainer the Owner adds in «مربیان» (15.2); the phone as typed, `09…`. */
export async function seedTrainer(
  api: APIRequestContext,
  trainer: { fullName: string; phoneNumber: string },
) {
  await send(api, await ownerToken(api), "POST", "/api/trainers", trainer);
}

/**
 * A member as the desk leaves them after a few days (15.5b): a 12-session plan with part of it
 * paid by card, and one visit with locker 7, checked out. The phone as typed, `09…`.
 */
export async function seedMemberWithRecords(
  api: APIRequestContext,
  member: { fullName: string; phoneNumber: string; birthDate: string },
) {
  const token = await ownerToken(api);
  await setPrices(api, token);

  const created = await send(api, token, "POST", "/api/members", { ...member, notes: null });
  const plan = await send(api, token, "POST", `/api/members/${created!.id}/subscriptions`, {
    sessionCount: 12,
  });
  await send(api, token, "POST", `/api/subscriptions/${plan!.id}/payments`, {
    amount: 500_000,
    method: "Card",
    referenceNumber: null,
  });

  const lockersResponse = await api.get("/api/lockers?PageSize=100", {
    headers: { Authorization: `Bearer ${token}` },
  });
  const lockers = (await lockersResponse.json()) as { items: { id: string; number: number }[] };
  const locker = lockers.items.find((item) => item.number === 7);
  expect(locker, "locker 7").toBeDefined();

  const visit = await send(api, token, "POST", `/api/members/${created!.id}/attendance/check-in`, {
    lockerId: locker!.id,
  });
  await send(api, token, "POST", `/api/attendance/${visit!.id}/check-out`, undefined);
}
