import { useState } from "react";
import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Pager } from "@/components/Pager";
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
 * The broadcasts sent, the latest first, each with how its messages went (BUSINESS_RULES.md §10
 * *The broadcast*). Each opens its own messages on «پیامک‌ها»; a stopped one can be continued.
 */
export function BroadcastList() {
  const [page, setPage] = useState(1);
  const broadcasts = useBroadcasts(page);

  if (broadcasts.isPending) {
    return <p className="text-muted-foreground">در حال بارگذاری…</p>;
  }

  if (broadcasts.isError) {
    return <Alert variant="destructive">{errorMessage(broadcasts.error)}</Alert>;
  }

  if (broadcasts.data.items.length === 0) {
    return <p className="text-muted-foreground">هنوز پیامک گروهی فرستاده نشده.</p>;
  }

  return (
    <div className="space-y-3">
      <ul className="space-y-3">
        {broadcasts.data.items.map((broadcast) => (
          <BroadcastItem key={broadcast.id} broadcast={broadcast} />
        ))}
      </ul>
      <Pager page={page} pageCount={broadcasts.data.pageCount} onPageChange={setPage} />
    </div>
  );
}

function BroadcastItem({ broadcast }: { broadcast: Broadcast }) {
  const [confirming, setConfirming] = useState(false);
  const resume = useResumeBroadcast();

  return (
    <li
      className="space-y-2 rounded-md border p-3 text-sm"
      aria-label={`پیامک گروهی ${formatDateTime(broadcast.createdAt)}`}
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
          <Button size="sm" variant="outline" onClick={() => setConfirming(true)}>
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
              size="sm"
              disabled={resume.isPending}
              onClick={() => resume.mutate(broadcast.id, { onSuccess: () => setConfirming(false) })}
            >
              {resume.isPending ? "در حال ثبت…" : "بله، ادامه بده"}
            </Button>
            <Button
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
    </li>
  );
}
