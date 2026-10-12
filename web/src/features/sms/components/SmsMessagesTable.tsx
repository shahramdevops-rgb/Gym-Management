import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { errorMessage } from "@/lib/errors";
import {
  emptyValue,
  formatDateTime,
  formatMoney,
  formatPhone,
  toPersianDigits,
} from "@/lib/format";

import { useResendSms, type SmsMessage } from "../api";
import {
  describeSmsError,
  isRefunded,
  smsDeliveryLabels,
  smsKindLabels,
  smsStatusLabels,
} from "../labels";

const columns = 6;

interface SmsMessagesTableProps {
  messages: SmsMessage[];
  onDone: (message: string) => void;
}

/** The SMS history's rows, the latest first, as the API sends them (BUSINESS_RULES.md §10). */
export function SmsMessagesTable({ messages, onDone }: SmsMessagesTableProps) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>زمان</TableHead>
          <TableHead>نوع</TableHead>
          <TableHead>گیرنده</TableHead>
          <TableHead>وضعیت</TableHead>
          <TableHead>هزینه</TableHead>
          <TableHead>عملیات</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {messages.map((message) => (
          <SmsMessageRow key={message.id} message={message} onDone={onDone} />
        ))}
      </TableBody>
    </Table>
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
      <TableRow joins={confirming ? "below" : undefined} className="align-top">
        <TableCell className="whitespace-nowrap">{formatDateTime(message.createdAt)}</TableCell>
        <TableCell label="نوع">
          <p>{smsKindLabels[message.kind]}</p>
          <p className="text-xs text-muted-foreground">{message.text}</p>
        </TableCell>
        <TableCell label="گیرنده">
          <p>{recipient}</p>
          <p className="text-xs text-muted-foreground" dir="ltr">
            {formatPhone(message.recipient)}
          </p>
        </TableCell>
        <TableCell label="وضعیت" className="space-y-1">
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
        </TableCell>
        <TableCell label="هزینه" className="space-y-1">
          <p>{message.costToman === null ? emptyValue : formatMoney(message.costToman)}</p>
          {isRefunded(message.delivery) && (
            <p className="text-xs text-muted-foreground">هزینه برگشت داده شد</p>
          )}
        </TableCell>
        <TableCell>
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
        </TableCell>
      </TableRow>
      {confirming && (
        <TableRow joins="above" className="bg-muted/30">
          <TableCell colSpan={columns} className="space-y-2 py-2">
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
          </TableCell>
        </TableRow>
      )}
    </>
  );
}
