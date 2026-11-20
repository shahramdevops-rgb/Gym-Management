import { fireEvent, screen } from "@testing-library/react";

import { gymToday } from "@/lib/format";
import { json, mockApi } from "@/test/mockApi";

import { renderSite, siteAccess, trainer } from "../test/renderSite";
import { addDays } from "../timetable/labels";

/*
 * A student's profile (BUSINESS_RULES.md §15 *The student's profile and every visit in the month*,
 * roadmap 15.7p): opened from the name, only what the trainer already sees.
 */

const trainerOnly = { member: null, trainer };
const memberId = "0199a000-0000-7000-8000-000000000101";

function studentsApi(student: object, periodSoonDays = 7) {
  return mockApi({
    "GET /api/site/me": () => json(200, trainerOnly),
    "GET /api/site/coaching/students": () =>
      json(200, {
        requests: [],
        periodSoonDays,
        students: [
          {
            memberId,
            fullName: "علی محمدی",
            phoneNumber: "+989121234567",
            since: "2026-10-01",
            isActive: true,
            periodEndsOn: null,
            ...student,
          },
        ],
      }),
  });
}

const periodLine = () => screen.getByText("دورهٔ مربی‌گری").nextElementSibling as HTMLElement;

describe("the student's profile", () => {
  it("Profile_FromTheName_ShowsPhoneSincePeriodAndTheTwoPages", async () => {
    studentsApi({ periodEndsOn: addDays(gymToday(), 11) });
    renderSite("/students", { access: siteAccess(trainerOnly) });

    fireEvent.click(await screen.findByRole("link", { name: "علی محمدی" }));

    expect(await screen.findByRole("heading", { name: "علی محمدی", level: 1 })).toBeVisible();
    // The one place the number shows (the developer, 1405/08/29), beside the call icon.
    expect(screen.getByRole("link", { name: "۰۹۱۲ ۱۲۳ ۴۵۶۷" })).toHaveAttribute(
      "href",
      "tel:+989121234567",
    );
    expect(screen.getByRole("link", { name: "تماس با علی محمدی" })).toHaveAttribute(
      "href",
      "tel:+989121234567",
    );
    expect(screen.getByText("شاگرد از").nextElementSibling).toHaveTextContent("۱۴۰۵/۰۷/۰۹");
    expect(periodLine()).toHaveTextContent(/^تا .+، ۱۲ روز مانده$/);
    expect(periodLine()).not.toHaveClass("text-warning-ink");
    expect(screen.getByRole("link", { name: "حضور" })).toHaveAttribute(
      "href",
      `/students/${memberId}/attendance`,
    );
    expect(screen.getByRole("link", { name: "برنامهٔ ثابت" })).toHaveAttribute(
      "href",
      `/students/${memberId}`,
    );
    expect(screen.getByRole("link", { name: "بازگشت" })).toHaveAttribute("href", "/students");
    // Nothing of the member's plan or debt (§15).
    expect(screen.queryByText(/اشتراک|بدهی/)).not.toBeInTheDocument();
  });

  it("Profile_PeriodEndingWithinTheSmsDays_InAmber", async () => {
    studentsApi({ periodEndsOn: addDays(gymToday(), 1) });
    renderSite(`/students/${memberId}/profile`, { access: siteAccess(trainerOnly) });

    await screen.findByRole("heading", { name: "علی محمدی", level: 1 });
    expect(periodLine()).toHaveTextContent("۲ روز مانده");
    expect(periodLine()).toHaveClass("text-warning-ink");
  });

  it("Profile_PeriodNotGiven_SaysSoInAmber", async () => {
    studentsApi({ periodEndsOn: null });
    renderSite(`/students/${memberId}/profile`, { access: siteAccess(trainerOnly) });

    await screen.findByRole("heading", { name: "علی محمدی", level: 1 });
    expect(periodLine()).toHaveTextContent("مشخص نشده است");
    expect(periodLine()).toHaveClass("text-warning-ink");
  });

  it("Profile_PeriodOver_SaysWhenAndGoesBackToTheInactiveList", async () => {
    studentsApi({ periodEndsOn: addDays(gymToday(), -3) });
    renderSite(`/students/${memberId}/profile`, { access: siteAccess(trainerOnly) });

    await screen.findByRole("heading", { name: "علی محمدی", level: 1 });
    expect(periodLine()).toHaveTextContent(/تمام شد$/);
    expect(screen.getByRole("link", { name: "بازگشت" })).toHaveAttribute(
      "href",
      "/students/inactive",
    );
  });

  it("Profile_DeactivatedMember_ShowsTheirInactiveBadge", async () => {
    studentsApi({ isActive: false, periodEndsOn: addDays(gymToday(), 20) });
    renderSite(`/students/${memberId}/profile`, { access: siteAccess(trainerOnly) });

    const heading = await screen.findByRole("heading", { name: /^علی محمدی/, level: 1 });
    expect(heading).toHaveTextContent("علی محمدیغیرفعال");
  });

  it("Profile_NotTheTrainersStudent_SendsBackToMyStudents", async () => {
    studentsApi({});
    renderSite("/students/0199a000-0000-7000-8000-000000000999/profile", {
      access: siteAccess(trainerOnly),
    });

    expect(
      await screen.findByRole("heading", { name: "شاگردان من (۱)", level: 1 }),
    ).toBeInTheDocument();
  });
});
