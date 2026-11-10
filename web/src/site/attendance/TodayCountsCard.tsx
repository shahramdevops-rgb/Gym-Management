import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

import { sitePaths } from "../paths";
import { useTrainerToday } from "./api";
import { DayTotalsBoxes } from "./DayTotalsBoxes";

/**
 * «امروز» on the trainer's home (BUSINESS_RULES.md §15 *More for the trainer*, 15.7d): today's
 * counts, the same as at the top of «حضور امروز» (opened from «سانس‌های امروز» below). Shown only
 * on a day with sessions; nothing shows while it loads or when it cannot be read, as «غایبان امروز».
 * Each box opens «حضور امروز» showing only its students (15.7f).
 */
export function TodayCountsCard() {
  const today = useTrainerToday();

  if (today.data === undefined || today.data.sessions.length === 0) {
    return null;
  }

  return (
    <Card accent={false} className="gap-3 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          امروز
        </CardTitle>
      </CardHeader>
      <CardContent>
        <DayTotalsBoxes
          totals={today.data.totals}
          label="جمع امروز"
          linkTo={(filter) => `${sitePaths.today}?show=${filter}`}
        />
      </CardContent>
    </Card>
  );
}
