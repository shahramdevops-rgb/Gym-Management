import { useState } from "react";
import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/errors";
import { formatDateTime, formatMoney, toPersianDigits } from "@/lib/format";

import { useBroadcasts, useResumeBroadcast, type Broadcast } from "../api";
import {
  broadcastAudienceLabels,
  broadcastStatusLabels,
  broadcastStopReasonLabels,
} from "../labels";

const statusBadges = {
  Sending: "secondary",
  Finished: "success",
  Stopped: "destructive",
} as const;

/**
 * The latest broadcast, only while it is sending or stopped (BUSINESS_RULES.md §10 *The broadcast*,
 * task 10.15): how far it got, and «ادامه ارسال» for a stopped one, which is the only place it can be
 * continued. Finished ones are not listed here: every message is on «پیامک‌ها», filtered by kind.
 * One broadcast sends at a time, so the latest is the only one that can still be going.
 */
export function LatestBroadcast() {
  const broadcasts = useBroadcasts(1);
  const latest = broadcasts.data?.items[0];

  if (broadcasts.isError) {
    return <Alert variant="destructive">{errorMessage(broadcasts.error)}</Alert>;
  }

  if (latest === undefined || latest.status === "Finished") {
    return null;
  }

  return <BroadcastStatus broadcast={latest} />;
}

function BroadcastStatus({ broadcast }: { broadcast: Broadcast }) {
  const [confirming, setConfirming] = useState(false);
  const resume = useResumeBroadcast();

  return (
    <div
      role="group"
      className="space-y-2 rounded-md border p-3 text-sm"
      aria-label={`آخرین پیامک گروهی ${formatDateTime(broadcast.createdAt)}`}
    >
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-medium">{formatDateTime(broadcast.createdAt)}</span>
        <span className="text-muted-foreground">{broadcastAudienceLabels[broadcast.audience]}</span>
        <Badge variant={statusBadges[broadcast.status]}>
          {broadcastStatusLabels[broadcast.status]}
        </Badge>
        {broadcast.stopReason !== null && (
          <span className="text-muted-foreground">
            {broadcastStopReasonLabels[broadcast.stopReason]}
          </span>
        )}
      </div>

      <p className="whitespace-pre-wrap">{broadcast.text}</p>

      <p className="text-muted-foreground">
        فرستاده: {toPersianDigits(broadcast.sent)} · ناموفق: {toPersianDigits(broadcast.failed)} ·
        نامعلوم: {toPersianDigits(broadcast.unknown)}
        {Number(broadcast.pending) > 0 && (
          <> · در حال ارسال: {toPersianDigits(broadcast.pending)}</>
        )}{" "}
        · هزینه: {formatMoney(broadcast.costToman)}
      </p>

      <div className="flex flex-wrap items-center gap-3">
        <Link
          className="text-primary underline"
          to={`${paths.smsMessages}?broadcast=${broadcast.id}`}
        >
          پیامک‌های این ارسال
        </Link>
        {broadcast.status === "Stopped" && !confirming && (
          <Button type="button" size="sm" variant="outline" onClick={() => setConfirming(true)}>
            ادامه ارسال
          </Button>
        )}
      </div>

      {confirming && (
        <div className="space-y-2 rounded-md border bg-muted/30 p-3">
          <p>
            همین متن به اعضای همین گروه که هنوز پیامکی از این ارسال نگرفته‌اند فرستاده می‌شود و
            هزینه دارد. پیامک‌های ناموفق را از «پیامک‌های این ارسال» دوباره بفرستید.
          </p>
          {resume.isError && <Alert variant="destructive">{errorMessage(resume.error)}</Alert>}
          <div className="flex flex-wrap gap-2">
            <Button
              type="button"
              size="sm"
              disabled={resume.isPending}
              onClick={() => resume.mutate(broadcast.id, { onSuccess: () => setConfirming(false) })}
            >
              {resume.isPending ? "در حال ثبت…" : "بله، ادامه بده"}
            </Button>
            <Button
              type="button"
              size="sm"
              variant="outline"
              disabled={resume.isPending}
              onClick={() => {
                setConfirming(false);
                resume.reset();
              }}
            >
              بازگشت
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
