import { Search, UserPlus } from "lucide-react";
import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router";

import { paths } from "@/app/paths";
import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { useDeskDialog } from "@/features/attendance/components/useDeskDialog";
import { Input } from "@/components/ui/input";
import { errorMessage } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";
import { normalizeInput } from "@/lib/normalize";
import { pageFromParams } from "@/lib/searchParams";
import { useDebouncedCallback } from "@/lib/useDebouncedCallback";

import { useCreateMember, useMemberList } from "../api";
import { MemberForm } from "../components/MemberForm";
import { MembersTable } from "../components/MembersTable";
import { searchMinLength } from "../schemas";
import { memberDraftFromSearch } from "../searchDraft";

/** Long enough to skip the keys of one word, short enough to feel immediate. */
export const searchDelayMs = 300;

/**
 * The member search: one box for a name or a phone number. Since roadmap 6.5.5 it checks nobody in —
 * that happens on the locker map, where the locker is chosen (BUSINESS_RULES.md §7). For someone
 * inside it still shows the locker and offers check-out.
 *
 * The search lives in the URL (`/search?q=علی&page=2`), not only in component state. Refreshing the
 * page, pressing back after opening a profile, or sharing the link all return to the same
 * results. The box updates the URL once typing pauses, and the query reads the URL.
 */
export function MemberSearchPage() {
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

  const createMember = useCreateMember();
  const [registering, setRegistering] = useState(false);

  const desk = useDeskDialog();
  const navigate = useNavigate();

  function goToPage(next: number) {
    setParams({ q, page: String(next) });
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-bold">جستجوی عضو</h2>
        <Button asChild>
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

          {ready && results.isSuccess && results.data.items.length === 0 && !registering && (
            <div className="space-y-3">
              <p className="text-muted-foreground">عضوی با این مشخصات پیدا نشد.</p>
              <Button
                onClick={() => {
                  setRegistering(true);
                }}
              >
                <UserPlus aria-hidden />
                ثبت این شخص
              </Button>
            </div>
          )}

          {registering && (
            // Registering here rather than on another screen: the search already holds the name or
            // the phone. The new member's profile opens next, where a plan is sold; letting them in
            // happens on the locker map, from the locker they are given (roadmap 6.5.5).
            <div className="space-y-3 rounded-lg border p-4">
              <h3 className="font-medium">عضو جدید</h3>
              <MemberForm
                // The search already holds the name or the phone; the desk should not type it
                // again with the person waiting.
                defaultValues={memberDraftFromSearch(q)}
                submitLabel="ثبت و ادامه"
                submittingLabel="در حال ثبت…"
                onSubmit={async (input) => {
                  const member = await createMember.mutateAsync(input);
                  void navigate(paths.member(member.id));
                }}
                actions={
                  <Button
                    type="button"
                    variant="ghost"
                    onClick={() => {
                      setRegistering(false);
                    }}
                  >
                    انصراف
                  </Button>
                }
              />
            </div>
          )}

          {ready && results.isSuccess && results.data.items.length > 0 && (
            <>
              <p className="text-sm text-muted-foreground">
                {toPersianDigits(results.data.totalCount)} نتیجه
              </p>
              <MembersTable
                members={results.data.items}
                deskActions={{
                  onCheckOut: (member) =>
                    desk.open({ kind: "checkOut", member, visit: member.currentVisit ?? null }),
                }}
              />
              <Pager page={page} pageCount={results.data.pageCount} onPageChange={goToPage} />
            </>
          )}
        </CardContent>
      </Card>

      {desk.dialog}
    </div>
  );
}
