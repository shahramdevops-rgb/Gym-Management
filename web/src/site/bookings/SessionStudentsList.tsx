import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { errorMessage } from "@/lib/errors";

import { ConfirmDialog } from "../components/ConfirmDialog";
import { Person } from "../components/Person";
import { formatDayLabel, formatSessionTimes, formatTaken } from "../timetable/labels";
import { VipBadge } from "../timetable/VipBadge";
import { useRemoveSessionStudent, useSessionStudents, type SessionStudentsItem } from "./api";

/**
 * «شاگردان سانس» of one date (BUSINESS_RULES.md §15 *Students in sessions*, roadmap 15.6b): each
 * session with its students, then the sessions removed since their students were put in them. The
 * trainer takes a student out of the date until the session starts, and is asked first.
 */
export function SessionStudentsList({ date }: { date: string }) {
  const roster = useSessionStudents(date);
  const remove = useRemoveSessionStudent(date);

  if (roster.data === undefined) {
    return roster.isError ? (
      <Alert variant="destructive">{errorMessage(roster.error)}</Alert>
    ) : (
      <LoadingState className="py-2" />
    );
  }

  if (roster.data.sessions.length === 0) {
    return <p className="text-sm text-muted-foreground">این روز سانسی ندارد.</p>;
  }

  return (
    <div className="space-y-3">
      {remove.isError && <Alert variant="destructive">{errorMessage(remove.error)}</Alert>}
      {roster.data.sessions.map((session) => {
        const times = formatSessionTimes(session);
        return (
          <section
            key={session.start + session.end + String(session.removed)}
            aria-label={times}
            className="space-y-2 rounded-lg border p-3"
          >
            <h3 className="flex flex-wrap items-center gap-2 text-sm font-semibold">
              {times}
              {session.isVip && <VipBadge />}
              <SessionNote session={session} />
            </h3>
            {session.students.length === 0 ? (
              <p className="text-xs text-muted-foreground">شاگردی در این سانس نیست.</p>
            ) : (
              <ul aria-label={`شاگردان ${times}`} className="divide-y">
                {session.students.map((student) => (
                  <li
                    key={student.memberId}
                    className="flex flex-wrap items-center justify-between gap-2 py-2 first:pt-0 last:pb-0"
                  >
                    <Person
                      fullName={student.fullName}
                      phoneNumber={student.phoneNumber}
                      detail={student.moved ? "فقط این روز" : undefined}
                    />
                    {!session.started && (
                      <ConfirmDialog
                        label="حذف از این روز"
                        question={`${student.fullName} از سانس ${times} ${formatDayLabel(date)} برداشته می‌شود؛ روزهای دیگرش همان می‌ماند. ادامه می‌دهید؟`}
                        pending={remove.isPending}
                        onConfirm={(close) =>
                          remove.mutate(
                            { memberId: student.memberId, start: session.start, end: session.end },
                            { onSettled: close },
                          )
                        }
                      />
                    )}
                  </li>
                ))}
              </ul>
            )}
          </section>
        );
      })}
    </div>
  );
}

/** «۳ از ۸ نفر», or why the session is not as its students were put in it. */
function SessionNote({ session }: { session: SessionStudentsItem }) {
  if (session.removed) {
    return <Badge variant="destructive">لغو شد</Badge>;
  }

  return (
    <>
      <span className="font-normal text-muted-foreground">
        {formatTaken(session.students.length, session.capacity ?? 0)}
      </span>
      {session.started && <Badge variant="secondary">شروع شده</Badge>}
    </>
  );
}
