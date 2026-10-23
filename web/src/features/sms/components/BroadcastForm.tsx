import { useState } from "react";

import { TextareaField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { errorMessage } from "@/lib/errors";
import { formatMoney, toPersianDigits } from "@/lib/format";

import {
  useBroadcastAudiences,
  usePreviewBroadcast,
  useSendBroadcast,
  useSmsCredit,
  type BroadcastAudience,
  type BroadcastPreview,
} from "../api";
import {
  broadcastMaxParts,
  broadcastMaxTextLength,
  broadcastMessage,
  normalizeBroadcastText,
  smsParts,
} from "../broadcastText";
import { broadcastAudienceLabels, broadcastStopReasonLabels } from "../labels";
import { SmsPreview } from "./SmsPreview";

const audiences: readonly BroadcastAudience[] = ["ActiveMembers", "MembersWithUsablePlan"];

/** What the page says after «بله، بفرست»: sending, or saved but stopped before its job began. */
export interface BroadcastNotice {
  tone: "success" | "error";
  text: string;
}

interface BroadcastFormProps {
  /** All SMS or the broadcast is off in the saved settings: nothing can be sent. */
  off: boolean;
  onSent: (notice: BroadcastNotice) => void;
}

/**
 * A new broadcast (BUSINESS_RULES.md §10 *The broadcast*): the group, the Owner's text with its
 * characters and parts counted as they type, then a confirmation with the figures before anything
 * is sent. The counter uses the longest first name in the group, as the send will.
 */
export function BroadcastForm({ off, onSent }: BroadcastFormProps) {
  const [audience, setAudience] = useState<BroadcastAudience>("ActiveMembers");
  const [text, setText] = useState("");
  const [preview, setPreview] = useState<BroadcastPreview | null>(null);
  const groups = useBroadcastAudiences();
  const previewing = usePreviewBroadcast();
  const sending = useSendBroadcast();

  const group =
    groups.data?.[audience === "ActiveMembers" ? "activeMembers" : "membersWithUsablePlan"];
  const longestName = group?.longestFirstName ?? "";
  const normalized = normalizeBroadcastText(text);
  const maxLength = broadcastMaxTextLength(longestName);
  const parts = smsParts(broadcastMessage(longestName, normalized));
  const tooLong = parts > broadcastMaxParts;
  const empty = group !== undefined && Number(group.recipients) === 0;

  function review() {
    previewing.mutate(
      { text: normalized, audience },
      { onSuccess: (figures) => setPreview(figures) },
    );
  }

  function send() {
    sending.mutate(
      { text: normalized, audience },
      {
        onSuccess: (broadcast) => {
          setPreview(null);
          setText("");
          // Saved, but the job runner could not take it: the API stops it at once.
          onSent(
            broadcast.status === "Stopped" && broadcast.stopReason !== null
              ? {
                  tone: "error",
                  text: `پیامک گروهی ثبت شد ولی ارسالش شروع نشد (${broadcastStopReasonLabels[broadcast.stopReason]})؛ «ادامه ارسال» را در همین کارت بزنید.`,
                }
              : {
                  tone: "success",
                  text: "پیامک گروهی در حال ارسال است؛ پیشرفتش در همین کارت دیده می‌شود.",
                },
          );
        },
      },
    );
  }

  return (
    <div className="space-y-4">
      {off && (
        <Alert role="status">
          پیامک گروهی خاموش است. برای فرستادن، «همهٔ پیامک‌ها» و همین کارت را روشن و ذخیره کنید.
        </Alert>
      )}

      <fieldset className="space-y-2">
        <legend className="text-sm font-medium">گیرندگان</legend>
        <div className="flex flex-wrap gap-x-6 gap-y-2">
          {audiences.map((value) => {
            const count =
              groups.data?.[value === "ActiveMembers" ? "activeMembers" : "membersWithUsablePlan"]
                .recipients;

            return (
              <label key={value} className="flex cursor-pointer items-center gap-2 text-sm">
                <input
                  type="radio"
                  name="audience"
                  className="size-4 accent-primary"
                  checked={audience === value}
                  onChange={() => setAudience(value)}
                />
                {broadcastAudienceLabels[value]}
                {count !== undefined && (
                  <span className="text-muted-foreground">({toPersianDigits(count)} نفر)</span>
                )}
              </label>
            );
          })}
        </div>
        <p className="text-sm text-muted-foreground">
          «اعضای دارای پلن»: اعضای فعالی که امروز پلن قابل‌استفاده دارند (نه فریز، نه تمام‌شده، نه
          پلنِ بعدی). عضو غیرفعال هیچ‌وقت نمی‌گیرد.
        </p>
      </fieldset>

      <div className="space-y-1">
        <TextareaField
          label="متن پیامک"
          rows={4}
          value={text}
          error={tooLong ? "متن بلند است: پیامک هر عضو باید در ۳ بخش جا شود." : undefined}
          onChange={(event) => setText(event.target.value)}
        />
        <p className="text-sm text-muted-foreground" aria-live="polite">
          {toPersianDigits(normalized.length)} از {toPersianDigits(Math.max(0, maxLength))} نویسه ·{" "}
          {toPersianDigits(parts)} بخش
        </p>
        <p className="text-sm text-muted-foreground">
          نام کوچک هر عضو در ابتدا («… عزیز،») و «لغو11» در انتها خودکار اضافه می‌شود. هر بخش جدا
          هزینه دارد.
        </p>
      </div>

      {empty && <Alert role="status">در این گروه کسی نیست.</Alert>}
      {previewing.isError && <Alert variant="destructive">{errorMessage(previewing.error)}</Alert>}

      {/* Drawn with the group's longest first name: the message the parts are counted on. */}
      <SmsPreview
        text={
          normalized.length === 0 ? (
            <span className="text-muted-foreground">پیامک، همان‌طور که به گوشی می‌رسد.</span>
          ) : (
            broadcastMessage(longestName || "علی", normalized)
          )
        }
        caption={`${toPersianDigits(parts)} بخش از ${toPersianDigits(broadcastMaxParts)} · با بلندترین نام کوچک این گروه`}
      />

      {/* type="button": the card sits inside the settings form, which this must not save. */}
      <Button
        type="button"
        disabled={off || empty || normalized.length === 0 || tooLong || previewing.isPending}
        onClick={review}
      >
        {previewing.isPending ? "در حال بررسی…" : "بررسی و ارسال"}
      </Button>

      {preview !== null && (
        <Dialog
          open
          onOpenChange={(open) => {
            if (!open && !sending.isPending) {
              setPreview(null);
              sending.reset();
            }
          }}
        >
          <DialogContent onOpenAutoFocus={(event) => event.preventDefault()}>
            <BroadcastConfirm
              audience={audience}
              preview={preview}
              pending={sending.isPending}
              error={sending.isError ? errorMessage(sending.error) : null}
              onConfirm={send}
              onCancel={() => {
                setPreview(null);
                sending.reset();
              }}
            />
          </DialogContent>
        </Dialog>
      )}
    </div>
  );
}

interface BroadcastConfirmProps {
  audience: BroadcastAudience;
  preview: BroadcastPreview;
  pending: boolean;
  error: string | null;
  onConfirm: () => void;
  onCancel: () => void;
}

/** The figures before anything is paid for: who, how many parts, about how much, and the message. */
function BroadcastConfirm({
  audience,
  preview,
  pending,
  error,
  onConfirm,
  onCancel,
}: BroadcastConfirmProps) {
  const credit = useSmsCredit();

  return (
    <>
      <DialogHeader>
        <DialogTitle>فرستادن پیامک گروهی</DialogTitle>
        <DialogDescription>
          به {toPersianDigits(preview.recipients)} نفر ({broadcastAudienceLabels[audience]}) فرستاده
          می‌شود.
        </DialogDescription>
      </DialogHeader>

      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
        <dt className="text-muted-foreground">کل بخش‌ها</dt>
        <dd>{toPersianDigits(preview.totalParts)}</dd>
        <dt className="text-muted-foreground">هزینه</dt>
        <dd>
          {preview.estimatedCostToman === null
            ? "برآورد نمی‌شود: هنوز پیامکی با هزینه فرستاده نشده"
            : `حدود ${formatMoney(preview.estimatedCostToman)}`}
        </dd>
        {credit.isSuccess && credit.data.remainingToman !== null && (
          <>
            <dt className="text-muted-foreground">اعتبار پنل</dt>
            <dd>{formatMoney(credit.data.remainingToman)}</dd>
          </>
        )}
      </dl>

      <div className="space-y-1">
        <p className="text-sm text-muted-foreground">
          بلندترین پیامک ({toPersianDigits(preview.longestMessageParts)} بخش):
        </p>
        <p
          className="rounded-md border bg-muted/30 p-3 text-sm whitespace-pre-wrap"
          aria-label="نمونهٔ پیامک"
        >
          {preview.sampleText}
        </p>
      </div>

      {error !== null && <Alert variant="destructive">{error}</Alert>}

      <div className="flex flex-wrap gap-2">
        <Button type="button" disabled={pending} onClick={onConfirm}>
          {pending ? "در حال ارسال…" : "بله، بفرست"}
        </Button>
        <Button type="button" variant="outline" disabled={pending} onClick={onCancel}>
          بازگشت
        </Button>
      </div>
    </>
  );
}
