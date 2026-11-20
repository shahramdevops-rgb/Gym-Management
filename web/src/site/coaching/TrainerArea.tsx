import { ChevronLeft } from "lucide-react";
import type { ReactNode } from "react";
import { Link } from "react-router";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { gymToday, toPersianDigits } from "@/lib/format";

import { sitePaths } from "../paths";
import { useStudents } from "./api";
import { periodMark, periodState, soonDaysOf, studentsOf } from "./period";

/**
 * «شاگردان من» on the trainer's home (BUSINESS_RULES.md §15 *A member and their trainer*, roadmap
 * 15.4b): since 1405/08/20 a card with the count, «شاگردان من (۱۲)», that opens the list on its own
 * page (`StudentsPage`), asked by the developer so the home stays short. Since 15.7l it counts the
 * active students only (§15 *«شاگردان من» in two parts*: the coaching runs, or the student is from
 * before the periods), those whose membership is inactive too. The active students whose coaching
 * period needs the trainer (ending within the SMS settings' days, 15.7n, or not given) are named
 * under it, since their marks are on the page:
 * «دورهٔ مینا رضایی رسیدگی می‌خواهد.», two names at most, then only how many («دورهٔ ۳ شاگرد رسیدگی
 * می‌خواهد.»; the wording the developer's, 1405/08/20).
 *
 * Right under it since 15.7p, «شاگردان غیرفعال (n)», those whose period is over and was not renewed,
 * opening their own page (`InactiveStudentsPage`); not shown while there is none, as «درخواست‌ها»
 * is not (§15 *The student's profile and every visit in the month*).
 *
 * The counts come from the same read as the waiting requests' card (`useStudents`), which the home
 * makes anyway, so the cards cost no request of their own.
 */
export function TrainerArea() {
  const students = useStudents();
  const today = gymToday();
  const list = students.data?.students;
  const active = list === undefined ? undefined : studentsOf(list, "active", today);
  const inactive = list === undefined ? [] : studentsOf(list, "inactive", today);
  const soonDays = students.data === undefined ? 0 : soonDaysOf(students.data);
  const needing =
    active?.filter(
      (student) => periodMark(periodState(student.periodEndsOn, today), soonDays) !== null,
    ) ?? [];

  return (
    <>
      <CountCard to={sitePaths.students} title="شاگردان من" count={active?.length}>
        {needing.length > 0 && (
          <p className="text-sm text-warning-ink">{needingText(needing.map((s) => s.fullName))}</p>
        )}
      </CountCard>
      {inactive.length > 0 && (
        <CountCard
          to={sitePaths.inactiveStudents}
          title="شاگردان غیرفعال"
          count={inactive.length}
        />
      )}
    </>
  );
}

/** A card that opens a page, its title with a count once it is known. */
function CountCard({
  to,
  title,
  count,
  children,
}: {
  to: string;
  title: string;
  count: number | undefined;
  children?: ReactNode;
}) {
  const withCount = count === undefined ? title : `${title} (${toPersianDigits(count)})`;

  return (
    <Link
      to={to}
      aria-label={withCount}
      className="block rounded-xl focus-visible:ring-[3px] focus-visible:ring-ring/30 focus-visible:outline-none"
    >
      <Card accent={false} className="gap-1 shadow-raised transition-colors hover:bg-muted/40">
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle role="heading" aria-level={2}>
            {title}
            {count !== undefined && (
              <span className="font-normal text-muted-foreground"> ({toPersianDigits(count)})</span>
            )}
          </CardTitle>
          <ChevronLeft aria-hidden className="size-5 text-muted-foreground" />
        </CardHeader>
        {children !== undefined && children !== false && <CardContent>{children}</CardContent>}
      </Card>
    </Link>
  );
}

/** Two names at most, then only how many: a long line of names would push the card down. */
function needingText(names: readonly string[]): string {
  const who =
    names.length === 1
      ? names[0]
      : names.length === 2
        ? `${names[0]} و ${names[1]}`
        : `${toPersianDigits(names.length)} شاگرد`;

  return `دورهٔ ${who} رسیدگی می‌خواهد.`;
}
