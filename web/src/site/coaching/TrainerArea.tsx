import { ChevronLeft } from "lucide-react";
import { Link } from "react-router";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { gymToday, toPersianDigits } from "@/lib/format";

import { sitePaths } from "../paths";
import { useStudents } from "./api";
import { periodMark, periodState } from "./period";

/**
 * «شاگردان من» on the trainer's home (BUSINESS_RULES.md §15 *A member and their trainer*, roadmap
 * 15.4b): since 1405/08/20 a card with the count, «شاگردان من (۱۲)», that opens the list on its own
 * page (`StudentsPage`), asked by the developer so the home stays short. Every student is counted,
 * those whose membership is inactive too, as the list shows them. The students whose coaching
 * period needs the trainer (15.7h: ending within three days, over, or not given) are named under
 * it, since their marks are on the page now: «دورهٔ مینا رضایی رسیدگی می‌خواهد.», two names at
 * most, then only how many («دورهٔ ۳ شاگرد رسیدگی می‌خواهد.»; the wording the developer's,
 * 1405/08/20).
 *
 * The count comes from the same read as the waiting requests' card (`useStudents`), which the home
 * makes anyway, so the card costs no request of its own.
 */
export function TrainerArea() {
  const students = useStudents();
  const list = students.data?.students;
  const today = gymToday();
  const needing =
    list?.filter((student) => periodMark(periodState(student.periodEndsOn, today)) !== null) ?? [];

  return (
    <Link
      to={sitePaths.students}
      aria-label={
        list === undefined ? "شاگردان من" : `شاگردان من (${toPersianDigits(list.length)})`
      }
      className="block rounded-xl focus-visible:ring-[3px] focus-visible:ring-ring/30 focus-visible:outline-none"
    >
      <Card accent={false} className="gap-1 shadow-raised transition-colors hover:bg-muted/40">
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle role="heading" aria-level={2}>
            شاگردان من
            {list !== undefined && (
              <span className="font-normal text-muted-foreground">
                {" "}
                ({toPersianDigits(list.length)})
              </span>
            )}
          </CardTitle>
          <ChevronLeft aria-hidden className="size-5 text-muted-foreground" />
        </CardHeader>
        {needing.length > 0 && (
          <CardContent>
            <p className="text-sm text-warning-ink">
              {needingText(needing.map((s) => s.fullName))}
            </p>
          </CardContent>
        )}
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
