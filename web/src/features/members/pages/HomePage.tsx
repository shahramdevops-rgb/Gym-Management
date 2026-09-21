import { Search, UserPlus } from "lucide-react";
import { useState } from "react";
import { Link, useSearchParams } from "react-router";

import { paths } from "@/app/paths";
import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { checkInResultMessage } from "@/features/attendance/checkInMessage";
import { useCheckIn } from "@/features/attendance/api";
import { errorMessage } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";
import { normalizeInput } from "@/lib/normalize";
import { pageFromParams } from "@/lib/searchParams";
import { useDebouncedCallback } from "@/lib/useDebouncedCallback";

import { useMemberList, type Member } from "../api";
import { MembersTable } from "../components/MembersTable";
import { searchMinLength } from "../schemas";

/** Long enough to skip the keys of one word, short enough to feel immediate. */
export const searchDelayMs = 300;

/**
 * The front desk's first screen: one box for a name or a phone number.
 *
 * The search lives in the URL (`/?q=علی&page=2`), not only in component state. Refreshing the
 * page, pressing back after opening a profile, or sharing the link all return to the same
 * results. The box updates the URL once typing pauses, and the query reads the URL.
 */
export function HomePage() {
  const [params, setParams] = useSearchParams();
  const q = params.get("q") ?? "";
  const page = pageFromParams(params);

  // What is in the box. It runs ahead of `q` while the user types.
  const [text, setText] = useState(q);

  // When the URL changes from outside (back button, a link), the box follows it. Adjusting
  // state during render is React's documented pattern for this; an effect would first render
  // the stale text.
  const [shownQ, setShownQ] = useState(q);
  if (q !== shownQ) {
    setShownQ(q);
    setText(q);
  }

  // `replace`: every pause while typing is not a step the back button should walk through.
  const commit = (value: string) =>
    setParams(value.trim() === "" ? {} : { q: value }, { replace: true });
  const debouncedCommit = useDebouncedCallback(commit, searchDelayMs);

  // The API normalizes too; doing it here also makes "علي" and "علی" one cache entry.
  const search = normalizeInput(q);
  const ready = search.length >= searchMinLength;
  const results = useMemberList({ search, page }, { enabled: ready });

  const checkIn = useCheckIn();
  const [checkingInId, setCheckingInId] = useState<string | null>(null);
  const [notice, setNotice] = useState<{ kind: "success" | "destructive"; text: string } | null>(
    null,
  );

  function goToPage(next: number) {
    setParams({ q, page: String(next) });
  }

  async function handleCheckIn(member: Member) {
    setNotice(null);
    setCheckingInId(member.id);
    try {
      const attendance = await checkIn.mutateAsync(member.id);
      setNotice({
        kind: "success",
        text: `${member.fullName}: ${checkInResultMessage(attendance)}`,
      });
    } catch (problem) {
      setNotice({ kind: "destructive", text: `${member.fullName}: ${errorMessage(problem)}` });
    } finally {
      setCheckingInId(null);
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-bold">جستجوی عضو</h2>
        <Button asChild variant="outline">
          <Link to={paths.newMember}>
            <UserPlus aria-hidden />
            عضو جدید
          </Link>
        </Button>
      </div>

      <form
        role="search"
        className="relative max-w-xl"
        onSubmit={(event) => {
          event.preventDefault();
          debouncedCommit.cancel();
          commit(text);
        }}
      >
        <Search
          className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
          aria-hidden
        />
        <Input
          type="search"
          aria-label="نام یا شماره موبایل"
          placeholder="نام یا شماره موبایل عضو…"
          className="h-11 ps-9 text-base"
          autoFocus
          value={text}
          onChange={(event) => {
            setText(event.target.value);
            debouncedCommit.run(event.target.value);
          }}
        />
      </form>

      <Card>
        <CardContent className="space-y-4">
          {notice !== null && (
            <Alert variant={notice.kind} role={notice.kind === "success" ? "status" : "alert"}>
              {notice.text}
            </Alert>
          )}

          {q.trim() === "" && (
            <p className="text-muted-foreground">
              نام، بخشی از نام، شماره موبایل یا دست‌کم ۴ رقم آن را بنویسید.
            </p>
          )}

          {q.trim() !== "" && !ready && (
            <p className="text-muted-foreground">
              برای جستجو دست‌کم {toPersianDigits(searchMinLength)} حرف وارد کنید.
            </p>
          )}

          {ready && results.isPending && <p className="text-muted-foreground">در حال جستجو…</p>}
          {ready && results.isError && (
            <Alert variant="destructive">{errorMessage(results.error)}</Alert>
          )}

          {ready && results.isSuccess && results.data.items.length === 0 && (
            <p className="text-muted-foreground">عضوی با این مشخصات پیدا نشد.</p>
          )}

          {ready && results.isSuccess && results.data.items.length > 0 && (
            <>
              <p className="text-sm text-muted-foreground">
                {toPersianDigits(results.data.totalCount)} نتیجه
              </p>
              <MembersTable
                members={results.data.items}
                onCheckIn={(member) => void handleCheckIn(member)}
                checkingInId={checkingInId}
              />
              <Pager page={page} pageCount={results.data.pageCount} onPageChange={goToPage} />
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
