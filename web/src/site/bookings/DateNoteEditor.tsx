import { CalendarDays, Pencil } from "lucide-react";
import { useId, useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { errorMessage } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";

import { ConfirmDialog } from "../components/ConfirmDialog";
import { formatDayLabel } from "../timetable/labels";
import { useRemoveSessionNote, useSetSessionNote, type SessionStudentsItem } from "./api";

/** The API's `SessionNoteText.MaxDateLength`: the box takes no more. */
const maxLength = 300;

/**
 * «یادداشت این روز» on one session of «شاگردان سانس» (BUSINESS_RULES.md §15 *Session notes*, roadmap
 * 15.7e): every student of the session sees it under that date. The trainer writes, changes or
 * removes it until the session ends («پاک کردن» asks first); after that it only shows.
 */
export function DateNoteEditor({
  date,
  session,
  times,
}: {
  date: string;
  session: SessionStudentsItem;
  /** The session's times as shown, «۱۳:۰۰ تا ۱۴:۰۰». */
  times: string;
}) {
  const [editing, setEditing] = useState(false);
  const [text, setText] = useState("");
  const set = useSetSessionNote(date);
  const remove = useRemoveSessionNote(date);
  const boxId = useId();
  const note = session.dateNote;
  const blank = text.trim() === "";

  if (editing) {
    return (
      <form
        className="space-y-2"
        onSubmit={(event) => {
          event.preventDefault();
          if (blank) {
            return;
          }
          set.mutate(
            { start: session.start, end: session.end, text },
            { onSuccess: () => setEditing(false) },
          );
        }}
      >
        <label htmlFor={boxId} className="text-xs font-semibold">
          یادداشت این روز
        </label>
        <Textarea
          id={boxId}
          autoFocus
          rows={3}
          maxLength={maxLength}
          placeholder="مثلاً امروز تمرین پا است؛ کفش مناسب بیاورید."
          value={text}
          onChange={(event) => setText(event.target.value)}
        />
        <p className="text-xs text-muted-foreground">
          همهٔ شاگردان این سانس آن را زیر همین روز می‌بینند. {toPersianDigits(text.length)} از{" "}
          {toPersianDigits(maxLength)} حرف.
        </p>
        {set.isError && <Alert variant="destructive">{errorMessage(set.error)}</Alert>}
        <div className="flex gap-2">
          <Button type="submit" size="sm" disabled={blank || set.isPending}>
            ذخیره
          </Button>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            disabled={set.isPending}
            onClick={() => {
              set.reset();
              setEditing(false);
            }}
          >
            انصراف
          </Button>
        </div>
      </form>
    );
  }

  const open = () => {
    setText(note ?? "");
    setEditing(true);
  };

  if (note === null || note === undefined) {
    return session.ended ? null : (
      <Button variant="outline" size="sm" onClick={open}>
        <CalendarDays aria-hidden />
        یادداشت این روز
      </Button>
    );
  }

  return (
    <div className="space-y-2">
      {remove.isError && <Alert variant="destructive">{errorMessage(remove.error)}</Alert>}
      {!session.ended && (
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" size="sm" aria-label={`ویرایش یادداشت ${times}`} onClick={open}>
            <Pencil aria-hidden />
            ویرایش یادداشت
          </Button>
          <ConfirmDialog
            label="پاک کردن یادداشت"
            question={`یادداشت سانس ${times} ${formatDayLabel(date)} پاک می‌شود و شاگردان دیگر آن را نمی‌بینند. ادامه می‌دهید؟`}
            pending={remove.isPending}
            onConfirm={(close) =>
              remove.mutate({ start: session.start, end: session.end }, { onSettled: close })
            }
          />
        </div>
      )}
    </div>
  );
}
