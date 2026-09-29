import { Search, UserPlus } from "lucide-react";
import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router";

import { paths } from "@/app/paths";
import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { useDeskDialog } from "@/features/attendance/components/useDeskDialog";
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
const searchDelayMs = 300;

type StatusFilter = "all" | "active" | "inactive";

const filters: { value: StatusFilter; label: string }[] = [
  { value: "all", label: "همه" },
  { value: "active", label: "فعال" },
  { value: "inactive", label: "غیرفعال" },
];

function statusFromParams(params: URLSearchParams): StatusFilter {
  const status = params.get("status");

  return status === "active" || status === "inactive" ? status : "all";
}

/**
 * Every member, by name, a page at a time, with one box above the list for a name or a phone
 * number. Inactive members are included by default (docs/BUSINESS_RULES.md §2), so staff can find
 * someone to reactivate. This page took over the member search screen: a search nobody matches
 * offers to register the person with what was typed, and someone inside can be checked out from
 * their row. Nobody is checked in here; that happens on the locker map, where the locker is chosen
 * (BUSINESS_RULES.md §7).
 *
 * The search, the filter and the page live in the URL (`/members?q=علی&status=active&page=2`), not
 * only in component state. Refreshing the page, pressing back after opening a profile, or sharing
 * the link all return to the same list. The box updates the URL once typing pauses, and the query
 * reads the URL.
 */
export function MembersPage() {
  const [params, setParams] = useSearchParams();
  const q = params.get("q") ?? "";
  const status = statusFromParams(params);
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

  // The API normalizes too; doing it here also makes "علي" and "علی" one cache entry.
  const search = normalizeInput(q);
  const searching = search.length >= searchMinLength;

  const members = useMemberList({
    search: searching ? search : undefined,
    isActive: status === "all" ? undefined : status === "active",
    page,
  });

  const createMember = useCreateMember();
  const [registering, setRegistering] = useState(false);

  const desk = useDeskDialog();
  const navigate = useNavigate();

  function show(next: { q?: string; status?: StatusFilter; page?: number }, replace = false) {
    const nextQ = next.q ?? q;
    const nextStatus = next.status ?? status;
    const nextPage = next.page ?? 1;

    const values: Record<string, string> = {};
    if (nextQ.trim() !== "") {
      values.q = nextQ;
    }
    if (nextStatus !== "all") {
      values.status = nextStatus;
    }
    if (nextPage > 1) {
      values.page = String(nextPage);
    }
    setParams(values, { replace });
  }

  // `replace`: every pause while typing is not a step the back button should walk through.
  const commit = (value: string) => {
    setRegistering(false);
    show({ q: value }, true);
  };
  const debouncedCommit = useDebouncedCallback(commit, searchDelayMs);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-bold">اعضا</h2>
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
          placeholder="نام، بخشی از نام یا شماره موبایل عضو…"
          className="h-11 ps-9 text-base"
          autoFocus
          value={text}
          onChange={(event) => {
            setText(event.target.value);
            debouncedCommit.run(event.target.value);
          }}
        />
      </form>

      {registering && (
        // Registering here rather than on another screen: the search already holds the name or
        // the phone. The new member's profile opens next, where a plan is sold; letting them in
        // happens on the locker map, from the locker they are given (roadmap 6.5.5).
        <Card>
          <CardHeader>
            <CardTitle>عضو جدید</CardTitle>
          </CardHeader>
          <CardContent>
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
                <Button type="button" variant="ghost" onClick={() => setRegistering(false)}>
                  انصراف
                </Button>
              }
            />
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader className="flex flex-wrap items-center justify-between gap-3">
          <CardTitle>
            {searching ? "نتیجه جستجو" : "فهرست اعضا"}
            {members.isSuccess && (
              <span className="ms-2 text-sm font-normal text-muted-foreground">
                ({toPersianDigits(members.data.totalCount)})
              </span>
            )}
          </CardTitle>
          <div role="group" aria-label="وضعیت عضویت" className="flex gap-1">
            {filters.map((filter) => (
              <Button
                key={filter.value}
                size="sm"
                variant={status === filter.value ? "secondary" : "ghost"}
                aria-pressed={status === filter.value}
                onClick={() => show({ status: filter.value })}
              >
                {filter.label}
              </Button>
            ))}
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {q.trim() !== "" && !searching && (
            <p className="text-sm text-muted-foreground">
              برای جستجو دست‌کم {toPersianDigits(searchMinLength)} حرف وارد کنید.
            </p>
          )}

          {members.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
          {members.isError && <Alert variant="destructive">{errorMessage(members.error)}</Alert>}

          {members.isSuccess && members.data.items.length === 0 && searching && (
            <div className="space-y-3">
              <p className="text-muted-foreground">عضوی با این مشخصات پیدا نشد.</p>
              {!registering && (
                <Button onClick={() => setRegistering(true)}>
                  <UserPlus aria-hidden />
                  ثبت این شخص
                </Button>
              )}
            </div>
          )}

          {members.isSuccess && members.data.items.length === 0 && !searching && (
            <p className="text-muted-foreground">
              {status === "all" ? "هنوز هیچ عضوی ثبت نشده است." : "عضوی با این وضعیت نیست."}
            </p>
          )}

          {members.isSuccess && members.data.items.length > 0 && (
            <>
              <MembersTable
                members={members.data.items}
                deskActions={{
                  onCheckOut: (member) =>
                    desk.open({ kind: "checkOut", member, visit: member.currentVisit ?? null }),
                }}
              />
              <Pager
                page={page}
                pageCount={members.data.pageCount}
                onPageChange={(next) => show({ page: next })}
              />
            </>
          )}
        </CardContent>
      </Card>

      {desk.dialog}
    </div>
  );
}
