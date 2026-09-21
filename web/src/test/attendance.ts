import type { Attendance, CurrentlyInside } from "@/features/attendance/api";

import { json } from "./mockApi";

const subscriptionId = "0199a000-0000-7000-8000-0000000000d1";
const lockerId = "0199a000-0000-7000-8000-0000000000e1";

export function openVisit(memberId: string): Attendance {
  return {
    id: "0199a000-0000-7000-8000-0000000000c1",
    memberId,
    subscriptionId,
    lockerId,
    lockerNumber: 3,
    checkedInAt: "2026-09-18T07:00:00Z",
    checkedOutAt: null,
    cancelledAt: null,
    autoClosedAt: null,
    createdAt: "2026-09-18T07:00:00Z",
  };
}

export function openVisitNoLocker(memberId: string): Attendance {
  return {
    ...openVisit(memberId),
    id: "0199a000-0000-7000-8000-0000000000c2",
    lockerId: null,
    lockerNumber: null,
  };
}

export function closedVisit(memberId: string): Attendance {
  return {
    ...openVisit(memberId),
    id: "0199a000-0000-7000-8000-0000000000c3",
    checkedOutAt: "2026-09-18T08:00:00Z",
  };
}

export function cancelledVisit(memberId: string): Attendance {
  return {
    ...openVisit(memberId),
    id: "0199a000-0000-7000-8000-0000000000c4",
    checkedOutAt: "2026-09-18T07:10:00Z",
    cancelledAt: "2026-09-18T07:10:00Z",
  };
}

export function autoClosedVisit(memberId: string): Attendance {
  return {
    ...openVisit(memberId),
    id: "0199a000-0000-7000-8000-0000000000c5",
    checkedOutAt: "2026-09-18T23:00:00Z",
    autoClosedAt: "2026-09-18T23:00:00Z",
  };
}

/** One page of GET /api/members/{memberId}/attendance. */
export function attendanceHistoryPage(items: Attendance[], totalCount = items.length): Response {
  return json(200, { items, page: 1, pageSize: 10, totalCount });
}

export function insideRow(memberFullName: string, attendance: Attendance): CurrentlyInside {
  return {
    attendanceId: attendance.id,
    memberId: attendance.memberId,
    memberFullName,
    lockerId: attendance.lockerId,
    lockerNumber: attendance.lockerNumber,
    checkedInAt: attendance.checkedInAt,
  };
}

/** One page of GET /api/attendance/currently-inside. */
export function currentlyInsidePage(items: CurrentlyInside[], totalCount = items.length): Response {
  return json(200, { items, page: 1, pageSize: 20, totalCount });
}
