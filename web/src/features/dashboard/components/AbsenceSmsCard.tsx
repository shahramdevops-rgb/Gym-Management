import { MessageSquareText } from "lucide-react";
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
import {
  formatDate,
  formatMoney,
  formatNumber,
  formatPercent,
  toPersianDigits,
} from "@/lib/format";
import type { Tone } from "@/lib/tone";

import {
  reportThresholds,
  type AbsenceSmsEffect,
  type AbsenceSmsMessage,
  type AbsenceSmsOutcome,
} from "../api";
import { absenceSmsRate } from "../figures";
import { Initial } from "./Initial";
import { StatCard } from "./StatCard";

const windowDays = toPersianDigits(reportThresholds.absenceSmsWindowDays);

/** The list's three groups, in the API's order: those who came back, those waiting, the rest. */
const groups: { outcome: AbsenceSmsOutcome; title: string; tone: Tone }[] = [
  { outcome: "CameBack", title: "برگشتند", tone: "green" },
  { outcome: "Waiting", title: "در انتظار", tone: "amber" },
  { outcome: "DidNotComeBack", title: "برنگشتند", tone: "red" },
];

/**
 * «اثر پیامک غیبت» (BUSINESS_RULES.md §12 *The absence SMS's effect*, task 10.19): of the absence
 * SMS sent in the range, what percent of the members came back within 14 days, came back ÷ (sent −
 * waiting). It says who came after the message, not because of it, and the hint says so in those
 * words. The corner holds the three counts and the cost; the button opens the names.
 */
export function AbsenceSmsCard({ effect }: { effect: AbsenceSmsEffect }) {
  const [open, setOpen] = useState(false);
  const sent = Number(effect.sent);
  const cameBack = Number(effect.cameBack);
  const waiting = Number(effect.waiting);
  const rate = absenceSmsRate(cameBack, sent, waiting);
  const average = effect.averageDaysToComeBack;

  return (
    <>
      <StatCard
        label="اثر پیامک غیبت"
        icon={MessageSquareText}
        value={formatPercent(rate)}
        progress={rate}
        hint={
          sent === 0
            ? "در این بازه پیامک غیبتی فرستاده نشده است."
            : `از اعضایی که در این بازه پیامک غیبت گرفتند، چند درصد تا ${windowDays} روز بعد به باشگاه آمدند.${
                average === null
                  ? ""
                  : ` میانگین: ${formatNumber(Number(average))} روز بعد از پیامک.`
              }${
                waiting > 0
                  ? ` «در انتظار»: هنوز ${windowDays} روزشان نگذشته و در درصد حساب نشده‌اند.`
                  : ""
              }`
        }
        breakdown={[
          { label: "برگشتند", value: `${formatNumber(cameBack)} نفر` },
          { label: "برنگشتند", value: `${formatNumber(Number(effect.didNotComeBack))} نفر` },
          { label: "در انتظار", value: `${formatNumber(waiting)} نفر` },
          { label: "هزینه", value: formatMoney(effect.costToman) },
        ]}
        action={
          sent > 0 ? (
            <Button variant="outline" size="sm" onClick={() => setOpen(true)}>
              فهرست نام‌ها
            </Button>
          ) : undefined
        }
      />
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent fullScreenOnPhone className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>اثر پیامک غیبت</DialogTitle>
            <DialogDescription>
              پیامک‌های غیبت این بازه، و اینکه عضو تا {windowDays} روز بعد از پیامک آمد یا نه.
            </DialogDescription>
          </DialogHeader>
          {groups.map((group) => (
            <MessageGroup
              key={group.outcome}
              title={group.title}
              tone={group.tone}
              messages={effect.messages.filter((message) => message.outcome === group.outcome)}
            />
          ))}
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

/** «بعد از ۳ روز آمد», «۵ روز مانده» or «نیامد» (§12 *The absence SMS's effect*). */
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
  }
}
