import { useState } from "react";
import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { isoDaysBefore } from "@/features/history/range";
import { formatDate, formatNumber, toPersianDigits } from "@/lib/format";
import type { Tone } from "@/lib/tone";

import { absenceSmsWindowDays } from "../absenceSmsText";
import {
  reportThresholds,
  type AbsenceSmsEffect,
  type AbsenceSmsMessage,
  type AbsenceSmsOutcome,
} from "../api";
import { Initial } from "./Initial";

const listDays = toPersianDigits(reportThresholds.absenceSmsListDays);

/** The list's groups, in the API's order: those who came back, those waiting, the rest. */
const groups: { outcome: AbsenceSmsOutcome; title: string; tone: Tone }[] = [
  { outcome: "CameBack", title: "برگشتند", tone: "green" },
  { outcome: "Waiting", title: "در انتظار", tone: "amber" },
  { outcome: "DidNotComeBack", title: "برنگشتند", tone: "red" },
  { outcome: "NotMeasurable", title: "قابل سنجش نیست", tone: "brown" },
];

/**
 * «فهرست نام‌ها» of «اثر پیامک غیبت» (BUSINESS_RULES.md §12 *The absence SMS's effect*): the
 * absence SMS of the last 30 days, today included, counted back from the report's own today so
 * the list and the server agree, each with what became of it. Only 30 days, so it stays short.
 */
export function AbsenceSmsNamesDialog({ effect }: { effect: AbsenceSmsEffect }) {
  const [open, setOpen] = useState(false);
  const since = isoDaysBefore(effect.to, reportThresholds.absenceSmsListDays - 1);
  const recent = effect.messages.filter((message) => message.sentOn >= since);

  return (
    <>
      <Button variant="outline" size="sm" onClick={() => setOpen(true)}>
        فهرست نام‌ها
      </Button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent fullScreenOnPhone className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>پیامک‌های غیبت {listDays} روز اخیر</DialogTitle>
            <DialogDescription>
              از {formatDate(since)} تا امروز، و اینکه عضو تا {absenceSmsWindowDays} روز بعد از
              پیامک آمد یا نه.
            </DialogDescription>
          </DialogHeader>
          {recent.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              در {listDays} روز اخیر پیامک غیبتی فرستاده نشده است.
            </p>
          ) : (
            groups.map((group) => (
              <MessageGroup
                key={group.outcome}
                title={group.title}
                tone={group.tone}
                messages={recent.filter((message) => message.outcome === group.outcome)}
              />
            ))
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}

function MessageGroup({
  title,
  tone,
  messages,
}: {
  title: string;
  tone: Tone;
  messages: AbsenceSmsMessage[];
}) {
  if (messages.length === 0) {
    return null;
  }

  return (
    <section className="space-y-1">
      <h3 className="text-sm font-bold">
        {title} ({formatNumber(messages.length)} نفر)
      </h3>
      <ul className="divide-y">
        {messages.map((message) => (
          <li
            key={message.notificationId}
            className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 py-2 text-sm"
          >
            <div className="flex items-center gap-2.5">
              <Initial name={message.fullName} tone={tone} />
              <div className="space-y-0.5">
                <Link to={paths.member(message.memberId)} className="font-medium hover:underline">
                  {message.fullName}
                </Link>
                <p className="text-xs text-muted-foreground">پیامک: {formatDate(message.sentOn)}</p>
              </div>
            </div>
            <div className="text-end text-xs">{followUpText(message)}</div>
          </li>
        ))}
      </ul>
    </section>
  );
}

/** «بعد از ۳ روز آمد», «۲ روز مانده», «نیامد» or «قابل سنجش نیست» (§12 *The absence SMS's effect*). */
function followUpText(message: AbsenceSmsMessage): string {
  switch (message.outcome) {
    case "CameBack": {
      const days = Number(message.daysToComeBack);
      return days === 0 ? "همان روز آمد" : `بعد از ${formatNumber(days)} روز آمد`;
    }
    case "Waiting": {
      const days = Number(message.daysLeft);
      return days === 0 ? "امروز روز آخر است" : `${formatNumber(days)} روز مانده`;
    }
    case "DidNotComeBack":
      return "نیامد";
    case "NotMeasurable":
      return "قابل سنجش نیست";
  }
}
