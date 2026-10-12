import { Search, SearchX, UserPlus } from "lucide-react";
import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router";

import { paths } from "@/app/paths";
import { PageHero } from "@/components/PageHero";
import { EmptyState, LoadingState } from "@/components/States";
import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { useDeskDialog } from "@/features/attendance/components/useDeskDialog";
import { choiceClass, filterEdge, toneAction } from "@/lib/edges";
import { errorMessage } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";
import { normalizeInput } from "@/lib/normalize";
import { pageFromParams } from "@/lib/searchParams";
import { useDebouncedCallback } from "@/lib/useDebouncedCallback";
import { cn } from "@/lib/utils";

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
 * Filters that each narrow the list on their own and combine with the status and with each other
 * (BUSINESS_RULES.md §2). Each is kept in the URL as `<param>=1`.
 */
type Toggle = "debtorsOnly" | "singleSessionOnly" | "planEndedOnly";

const toggles: { value: Toggle; param: string; label: string }[] = [
  { value: "debtorsOnly", param: "debt", label: "بدهکار" },
  { value: "singleSessionOnly", param: "single", label: "تک‌جلسه" },
  { value: "planEndedOnly", param: "ended", label: "پلن تمام‌شده" },
];

/** A filter button on a phone: small enough that all six share one line (13.1b). */
const phoneChoice = "max-lg:h-7 max-lg:px-1 max-lg:text-xs";

type ToggleState = Record<Toggle, boolean>;

function togglesFromParams(params: URLSearchParams): ToggleState {
  return {
    debtorsOnly: params.get("debt") === "1",
    singleSessionOnly: params.get("single") === "1",
    planEndedOnly: params.get("ended") === "1",
  };
}

/** What an empty list says when a toggle is on: the narrowest one names who is missing. */
function emptyToggleMessage(on: ToggleState): string | null {
  if (on.singleSessionOnly) {
    return "عضوی با آخرین ورود تک‌جلسه نیست.";
  }
  if (on.planEndedOnly) {
    return "عضوی با پلن تمام‌شده نیست.";
  }
  if (on.debtorsOnly) {
    return "عضو بدهکاری نیست.";
  }
  return null;
}

/**
 * Every member, whoever came last first, a page at a time, with one box above the list for a name or a phone
 * number. Inactive members are included by default (docs/BUSINESS_RULES.md §2), so staff can find
 * someone to reactivate. This page took over the member search screen: a search nobody matches
 * offers to register the person with what was typed, and someone inside can be checked out from
 * their row. Nobody is checked in here; that happens on the locker map, where the locker is chosen
 * (BUSINESS_RULES.md §7).
 *
 * The search, the filters and the page live in the URL (`/members?q=علی&status=active&debt=1&page=2`),
 * not only in component state. Refreshing the page, pressing back after opening a profile, or sharing
 * the link all return to the same list. The box updates the URL once typing pauses, and the query
 * reads the URL.
 *
 * «بدهکار», «تک‌جلسه» and «پلن تمام‌شده» are toggles rather than more statuses, so they combine
 * with the status and with each other: inactive members who still owe is a list the desk chases.
 * The API filters before paging (roadmap 6.5.20, 6.5.34).
 */
export function MembersPage() {
  const [params, setParams] = useSearchParams();
  const q = params.get("q") ?? "";
  const status = statusFromParams(params);
  const on = togglesFromParams(params);
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
    debtorsOnly: on.debtorsOnly || undefined,
    singleSessionOnly: on.singleSessionOnly || undefined,
    planEndedOnly: on.planEndedOnly || undefined,
    page,
  });

  const createMember = useCreateMember();
  const [registering, setRegistering] = useState(false);

  const desk = useDeskDialog();
  const navigate = useNavigate();

  function show(
    next: { q?: string; status?: StatusFilter; toggles?: Partial<ToggleState>; page?: number },
    replace = false,
  ) {
    const nextQ = next.q ?? q;
    const nextStatus = next.status ?? status;
    const nextOn = { ...on, ...next.toggles };
    const nextPage = next.page ?? 1;

    const values: Record<string, string> = {};
    if (nextQ.trim() !== "") {
      values.q = nextQ;
    }
    if (nextStatus !== "all") {
      values.status = nextStatus;
    }
    for (const toggle of toggles) {
      if (nextOn[toggle.value]) {
        values[toggle.param] = "1";
      }
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
      {/* The search on the banner, like the name search on the locker map's (13.2h). */}
      <PageHero
        title="اعضا"
        spreadOnPhone
        actions={
          // Edged in the page's tone like the search beside it, with no fill (13.2i, asked by the
          // developer).
          <Button asChild variant="ghost" className={toneAction}>
            <Link to={paths.newMember}>
              <UserPlus aria-hidden />
              عضو جدید
            </Link>
          </Button>
        }
      >
        <form
          role="search"
          // As wide as a name or a number needs (asked by the developer, 13.2i). On a phone, beside
          // the title, in the room the title leaves (asked by the developer, 13.1b); «عضو جدید»
          // goes under them.
          className="relative max-w-sm max-lg:min-w-0 max-lg:grow max-lg:basis-48"
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
            // Edged like every filter box, the thick edge at its end (13.2i).
            className={cn(filterEdge, "h-10 ps-9 text-base")}
            autoFocus
            value={text}
            onChange={(event) => {
              setText(event.target.value);
              debouncedCommit.run(event.target.value);
            }}
          />
        </form>
      </PageHero>

      {registering && (
        // Registering here rather than on another screen: the search already holds the name or
        // the phone. The new member's profile opens next, where a plan is sold; letting them in
        // happens on the locker map, from the locker they are given (roadmap 6.5.5).
        <Card edge>
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

      <Card edge>
        <CardHeader className="flex flex-wrap items-center justify-between gap-3">
          <CardTitle>
            {searching ? "نتیجه جستجو" : "فهرست اعضا"}
            {members.isSuccess && (
              <span className="ms-2 text-sm font-normal text-muted-foreground">
                ({toPersianDigits(members.data.totalCount)})
              </span>
            )}
          </CardTitle>
          {/* On a phone, every filter on one line under the title (asked by the developer, 13.1b):
              smaller and closer together. */}
          <div className="flex flex-wrap items-center gap-2 max-lg:flex-nowrap max-lg:gap-1">
            <div role="group" aria-label="وضعیت عضویت" className="flex gap-1 max-lg:gap-px">
              {filters.map((filter) => (
                <Button
                  key={filter.value}
                  size="sm"
                  variant="ghost"
                  className={cn(choiceClass(status === filter.value, "end"), phoneChoice)}
                  aria-pressed={status === filter.value}
                  onClick={() => show({ status: filter.value })}
                >
                  {filter.label}
                </Button>
              ))}
            </div>
            <span className="h-5 w-px bg-border" aria-hidden />
            <div
              role="group"
              aria-label="فیلترها"
              className="flex flex-wrap gap-1 max-lg:flex-nowrap max-lg:gap-px"
            >
              {toggles.map((toggle) => (
                <Button
                  key={toggle.value}
                  size="sm"
                  variant="ghost"
                  className={cn(choiceClass(on[toggle.value], "end"), phoneChoice)}
                  aria-pressed={on[toggle.value]}
                  onClick={() => show({ toggles: { [toggle.value]: !on[toggle.value] } })}
                >
                  {toggle.label}
                </Button>
              ))}
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {q.trim() !== "" && !searching && (
            <p className="text-sm text-muted-foreground">
              برای جستجو دست‌کم {toPersianDigits(searchMinLength)} حرف وارد کنید.
            </p>
          )}

          {members.isPending && <LoadingState />}
          {members.isError && <Alert variant="destructive">{errorMessage(members.error)}</Alert>}

          {members.isSuccess && members.data.items.length === 0 && searching && (
            <EmptyState
              icon={SearchX}
              action={
                !registering && (
                  <Button onClick={() => setRegistering(true)}>
                    <UserPlus aria-hidden />
                    ثبت این شخص
                  </Button>
                )
              }
            >
              عضوی با این مشخصات پیدا نشد.
            </EmptyState>
          )}

          {members.isSuccess && members.data.items.length === 0 && !searching && (
            <EmptyState>
              {emptyToggleMessage(on) ??
                (status === "all" ? "هنوز هیچ عضوی ثبت نشده است." : "عضوی با این وضعیت نیست.")}
            </EmptyState>
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
