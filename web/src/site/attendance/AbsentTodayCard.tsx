import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

import { Person } from "../components/Person";
import { formatSessionTimes } from "../timetable/labels";
import { useTrainerToday } from "./api";
import { cameAfterLabel, formatClock } from "./labels";

/**
 * «غایبان امروز» on the trainer's home (BUSINESS_RULES.md §15 *Attendance for the trainer*: the
 * absence alert is in the app only, no SMS): the absent students of today's sessions that have
 * ended. Shown only when there is one; it goes the next day, and the student's «حضور» keeps them.
 * Nothing shows while it loads or when it cannot be read: «حضور امروز» says so.
 */
export function AbsentTodayCard() {
  const today = useTrainerToday();

  const absent = (today.data?.sessions ?? [])
    .filter((session) => session.ended && !session.removed)
    .flatMap((session) =>
      session.students
        .filter((student) => student.state === "Absent")
        .map((student) => ({ session, student })),
    );

  if (absent.length === 0) {
    return null;
  }

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          غایبان امروز
        </CardTitle>
      </CardHeader>
      <CardContent>
        <ul aria-label="غایبان امروز" className="divide-y">
          {absent.map(({ session, student }) => (
            <li
              key={`${student.memberId}-${session.start}-${session.end}`}
              className="py-2 first:pt-0 last:pb-0"
            >
              <Person
                fullName={student.fullName}
                phoneNumber={student.phoneNumber}
                detail={
                  student.cameAfter && student.checkedInAt !== null
                    ? `${formatSessionTimes(session)} · ${cameAfterLabel} ${formatClock(student.checkedInAt)}`
                    : formatSessionTimes(session)
                }
              />
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
