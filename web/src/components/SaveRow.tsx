import type { ReactNode } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/errors";

/** What the last press of «ذخیره» did on an edit form, until the next edit clears it. */
export type SaveNotice = "saved" | "unchanged" | null;

interface SaveRowProps {
  /** The form's buttons: «ذخیره» first, then any «انصراف». */
  children: ReactNode;
  /** A server refusal that belongs to no field (the form's `errors.root.server`). */
  error?: string;
  notice?: SaveNotice;
  /** Someone else saved first: a `StaleAlert`, or nothing while the form is current. */
  stale?: ReactNode;
}

/**
 * The row of «ذخیره» on every edit form (roadmap 13.2c, as «تنظیمات پیامک» did first on
 * 1405/07/16): the buttons, and beside them every outcome of the last press — someone else saved,
 * a server error, nothing changed, saved — so it is shown where the user is looking, not at the
 * top of a page they may have scrolled away from. One outcome at a time, the most pressing first.
 */
export function SaveRow({ children, error, notice = null, stale }: SaveRowProps) {
  const status =
    stale !== undefined ? (
      stale
    ) : error !== undefined ? (
      <Alert variant="destructive">{error}</Alert>
    ) : notice !== null ? (
      <SaveNoticeAlert notice={notice} />
    ) : null;

  return (
    <div className="flex flex-wrap items-center gap-2">
      {children}
      {status !== null && (
        <div className="min-w-48 flex-1 [&>[data-slot=alert]]:py-2">{status}</div>
      )}
    </div>
  );
}

/**
 * The words for a `SaveNotice`, or nothing. On its own only where a form is one box and its
 * buttons in a row (a rename), so the line goes right under them instead.
 */
export function SaveNoticeAlert({ notice }: { notice: SaveNotice }) {
  if (notice === "saved") {
    return (
      <Alert variant="success" role="status">
        تغییرها ثبت شد.
      </Alert>
    );
  }

  return notice === "unchanged" ? (
    <Alert role="status">تغییری نداده‌اید؛ چیزی ذخیره نشد.</Alert>
  ) : null;
}

interface StaleAlertProps {
  /** The feature's «changed concurrently» code, e.g. `Members.ChangedConcurrently`. */
  code: string;
  reloading: boolean;
  onReload: () => void;
}

/**
 * Someone else saved this record after it was opened: the save was refused rather than writing
 * over theirs, and one press loads their version into the form. Given to `SaveRow` as `stale`.
 */
export function StaleAlert({ code, reloading, onReload }: StaleAlertProps) {
  return (
    <Alert variant="destructive">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <span>{errorMessage({ code })}</span>
        <Button type="button" size="sm" variant="outline" disabled={reloading} onClick={onReload}>
          بارگذاری اطلاعات تازه
        </Button>
      </div>
    </Alert>
  );
}
