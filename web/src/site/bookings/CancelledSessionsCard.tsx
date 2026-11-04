import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";

import { formatDayLabel, formatSessionTimes } from "../timetable/labels";
import { useMyCancellations, useSeeMyCancellations } from "./api";

/**
 * «سانس لغو شد» at the top of an active member's home (BUSINESS_RULES.md §15 *Timetable and
 * attendance*, roadmap 15.6c): the sessions their trainer cancelled, until they press «دیدم» or the
 * date passes. A phone notification is not reliable from Iran for the app, so this is where the
 * member hears of it, with the SMS for a near date. Nothing shows while it loads, when there is
 * nothing to tell, or when it cannot be read: «سانس‌های من» still marks the dates «لغو شد».
 */
export function CancelledSessionsCard() {
  const cancelled = useMyCancellations();
  const see = useSeeMyCancellations();
  const sessions = cancelled.data?.sessions ?? [];
  const latest = cancelled.data?.latest ?? null;

  if (sessions.length === 0 || latest === null) {
    return null;
  }

  return (
    <Card accent={false} className="gap-3 border-destructive/30 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          سانس لغو شد
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <p className="text-sm text-muted-foreground">
          {sessions.length === 1
            ? "مربی‌تان این سانس را لغو کرد."
            : "مربی‌تان این سانس‌ها را لغو کرد."}
        </p>
        <ul aria-label="سانس‌های لغوشده" className="grid grid-cols-3 gap-2">
          {sessions.map((session) => (
            <li
              key={session.date + session.start + session.end}
              className="space-y-1 rounded-lg border p-2"
            >
              <p className="text-xs font-semibold">{formatDayLabel(session.date)}</p>
              <p className="text-xs">{formatSessionTimes(session)}</p>
            </li>
          ))}
        </ul>
        {see.isError && <Alert variant="destructive">{errorMessage(see.error)}</Alert>}
        <Button
          variant="outline"
          className="w-full"
          disabled={see.isPending}
          onClick={() => see.mutate(latest)}
        >
          دیدم
        </Button>
      </CardContent>
    </Card>
  );
}
