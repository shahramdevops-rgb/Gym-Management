import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/errors";
import {
  emptyValue,
  formatDateTime,
  formatMoney,
  formatPhone,
  toPersianDigits,
} from "@/lib/format";

import { useResendSms, type SmsMessage } from "../api";
import { describeSmsError, smsDeliveryLabels, smsKindLabels, smsStatusLabels } from "../labels";

const columns = 6;

interface SmsMessagesTableProps {
  messages: SmsMessage[];
  onDone: (message: string) => void;
}

/** The SMS history's rows, the latest first, as the API sends them (BUSINESS_RULES.md §10). */
export function SmsMessagesTable({ messages, onDone }: SmsMessagesTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">زمان</th>
            <th className="py-2 text-start font-medium">نوع</th>
            <th className="py-2 text-start font-medium">گیرنده</th>
            <th className="py-2 text-start font-medium">وضعیت</th>
            <th className="py-2 text-start font-medium">هزینه</th>
            <th className="py-2 text-start font-medium">عملیات</th>
          </tr>
        </thead>
        <tbody>
          {messages.map((message) => (
            <SmsMessageRow key={message.id} message={message} onDone={onDone} />
          ))}
        </tbody>
      </table>
    </div>
  );
}

const statusBadges = {
  Sent: "success",
  Failed: "destructive",
  Unknown: "outline",
  Pending: "secondary",
} as const;

interface SmsMessageRowProps {
  message: SmsMessage;
  onDone: (message: string) => void;
}

/**
 * One SMS. A failed or unknown one can be resent, after a confirmation under the row: it costs
 * money, and an unknown one may already have arrived (§10 *Sending*).
 */
function SmsMessageRow({ message, onDone }: SmsMessageRowProps) {
  const [confirming, setConfirming] = useState(false);
  const resend = useResendSms();

  const recipient = message.memberName ?? "مالک";
  const context = `پیامک ${smsKindLabels[message.kind]} برای ${recipient}`;
  const canResend = message.status === "Failed" || message.status === "Unknown";
  const error = describeSmsError(message.errorCode);

  async function send() {
    const result = await resend.mutateAsync(message.id).catch(() => null);
    if (result === null) {
      return;
    }

    setConfirming(false);
    onDone(
      result.status === "Sent"
        ? `${context} دوباره فرستاده شد.`
        : `${context} باز هم فرستاده نشد: ${smsStatusLabels[result.status]}.`,
    );
  }

  return (
    <>
      <tr className="border-b align-top">
        <td className="py-2 whitespace-nowrap">{formatDateTime(message.createdAt)}</td>
        <td className="py-2">
          <p>{smsKindLabels[message.kind]}</p>
          <p className="text-xs text-muted-foreground">{message.text}</p>
        </td>
        <td className="py-2">
          <p>{recipient}</p>
          <p className="text-xs text-muted-foreground" dir="ltr">
            {formatPhone(message.recipient)}
          </p>
        </td>
        <td className="py-2 space-y-1">
          <Badge variant={statusBadges[message.status]}>{smsStatusLabels[message.status]}</Badge>
          {message.delivery !== null && (
            <p className="text-xs text-muted-foreground">{smsDeliveryLabels[message.delivery]}</p>
          )}
          {error !== null && <p className="text-xs text-destructive">{error}</p>}
          {Number(message.attempts) > 1 && (
            <p className="text-xs text-muted-foreground">
              {toPersianDigits(message.attempts)} بار تلاش
            </p>
          )}
        </td>
        <td className="py-2">
          {message.costToman === null ? emptyValue : formatMoney(message.costToman)}
        </td>
        <td className="py-2">
          {canResend && (
            <Button
              size="sm"
              variant="outline"
              aria-label={`ارسال دوباره ${context}`}
              onClick={() => {
                resend.reset();
                setConfirming((current) => !current);
              }}
            >
              ارسال دوباره
            </Button>
          )}
        </td>
      </tr>
      {confirming && (
        <tr className="border-b bg-muted/30">
          <td colSpan={columns} className="space-y-2 py-2">
            {message.status === "Unknown" ? (
              <Alert variant="destructive">
                معلوم نیست این پیامک رسیده یا نه: ممکن است رسیده باشد. با ارسال دوباره ممکن است
                گیرنده آن را دو بار بگیرد و هزینه‌اش دو بار حساب شود.
              </Alert>
            ) : (
              <p className="text-sm">
                همین پیامک، با همان متن و به همان شماره، یک بار دیگر فرستاده می‌شود و هزینه دارد.
              </p>
            )}
            {resend.isError && <Alert variant="destructive">{errorMessage(resend.error)}</Alert>}
            <div className="flex flex-wrap gap-2">
              <Button size="sm" disabled={resend.isPending} onClick={() => void send()}>
                {resend.isPending ? "در حال ارسال…" : "بفرست"}
              </Button>
              <Button
                size="sm"
                variant="outline"
                disabled={resend.isPending}
                onClick={() => setConfirming(false)}
              >
                انصراف
              </Button>
            </div>
          </td>
        </tr>
      )}
    </>
  );
}
