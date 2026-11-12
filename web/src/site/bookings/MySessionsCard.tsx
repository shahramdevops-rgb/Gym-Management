import { Link } from "react-router";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";

import { sitePaths } from "../paths";
import { formatDayLabel, formatSessionTimes } from "../timetable/labels";
import { useMySessions } from "./api";
import { SessionNotes } from "../timetable/SessionNotes";
import { PlaceBadges } from "./PlaceBadges";

/** The next dates on the home; the rest, and leaving or moving one, on «سانس‌های من». */
const shownOnHome = 3;

/**
 * «سانس‌های من» on an active member's home (BUSINESS_RULES.md §15 *Students in sessions*, roadmap
 * 15.6b): the next dates their trainer put them in, not those they left (the full page lists those,
 * to take back). Shown only with a trainer now; «مربی من» says the rest. Nothing is shown while it
 * loads, so a member without a trainer never sees it flash.
 */
export function MySessionsCard() {
  const mine = useMySessions();
  const coming = mine.data?.sessions.filter((place) => !place.left) ?? [];

  if (mine.data === undefined && !mine.isError) {
    return null;
  }
  if (mine.data !== undefined && mine.data.trainerName === null) {
    return null;
  }

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          سانس‌های من
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {mine.data === undefined ? (
          <Alert variant="destructive">{errorMessage(mine.error)}</Alert>
        ) : (
          <>
            {coming.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                {mine.data.schedule === null
                  ? "مربی‌تان هنوز برنامهٔ سانسی برایتان نچیده است."
                  : "سانسی در پیش ندارید."}
              </p>
            ) : (
              <ul aria-label="سانس‌های بعدی" className="grid grid-cols-3 gap-2">
                {coming.slice(0, shownOnHome).map((place) => (
                  <li
                    key={place.date + place.start + place.end}
                    className="space-y-1 rounded-lg border p-2"
                  >
                    <p className="text-xs font-semibold">{formatDayLabel(place.date)}</p>
                    <p className="text-xs">{formatSessionTimes(place)}</p>
                    <p className="flex flex-wrap gap-1">
                      <PlaceBadges place={place} />
                    </p>
                    <SessionNotes note={place.note} dateNote={place.dateNote} compact />
                  </li>
                ))}
              </ul>
            )}
            {/* Also when every date is left: the page is where «می‌آیم» takes one back. */}
            {mine.data.sessions.length > 0 && (
              <Button asChild variant="outline" className="w-full">
                <Link to={sitePaths.mySessions}>همهٔ سانس‌ها</Link>
              </Button>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
