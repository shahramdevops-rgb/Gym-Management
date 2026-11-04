import { CalendarDays } from "lucide-react";
import { Link } from "react-router";

import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";

import { sitePaths } from "../paths";
import { useTimetable } from "./api";
import { formatSessionTimes, formatTaken } from "./labels";
import { VipBadge } from "./VipBadge";

/**
 * The trainer's sessions today on the home screen, each with its students out of its capacity
 * (15.6b), and the way into «تمام سانس‌های من» (BUSINESS_RULES.md §15 *The trainer's sessions*,
 * roadmap 15.6a).
 */
export function TimetableCard() {
  const timetable = useTimetable();
  const today = timetable.data?.days[0];

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          سانس‌های امروز
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {timetable.data === undefined ? (
          timetable.isError ? (
            <Alert variant="destructive">{errorMessage(timetable.error)}</Alert>
          ) : (
            <LoadingState className="py-2" />
          )
        ) : today === undefined || today.sessions.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            {timetable.data.pattern === null
              ? "هنوز برنامهٔ سانسی نساخته‌اید."
              : "امروز سانسی ندارید."}
          </p>
        ) : (
          <ul aria-label="سانس‌های امروز" className="flex flex-wrap gap-1">
            {today.sessions.map((session) => (
              <li
                key={session.start + session.end}
                className="flex items-center gap-1 rounded-md bg-muted px-2 py-1 text-sm"
              >
                {formatSessionTimes(session)} · {formatTaken(session.students, session.capacity)}
                {session.isVip && <VipBadge />}
              </li>
            ))}
          </ul>
        )}
        <Button asChild variant="outline" className="w-full">
          <Link to={sitePaths.timetable}>
            <CalendarDays aria-hidden />
            تمام سانس‌های من
          </Link>
        </Button>
      </CardContent>
    </Card>
  );
}
