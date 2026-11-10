import { Search } from "lucide-react";
import { useState } from "react";
import { useSearchParams } from "react-router";

import { PageHero } from "@/components/PageHero";
import { Pager } from "@/components/Pager";
import { EmptyState, LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { searchMinLength } from "@/features/members/schemas";
import { filterEdge } from "@/lib/edges";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatDateTime, formatPhone, toPersianDigits } from "@/lib/format";
import { normalizeInput } from "@/lib/normalize";
import { pageFromParams } from "@/lib/searchParams";
import { useDebouncedCallback } from "@/lib/useDebouncedCallback";
import { cn } from "@/lib/utils";

import { useSiteUserList } from "../api";

/** As the member list's box: long enough to skip the keys of one word. */
const searchDelayMs = 300;

/**
 * «کاربران سایت» (BUSINESS_RULES.md §16 *Site users*): people who signed up on the site and have
 * had no plan and no visit, newest first, with what they entered there. For reading only, and the
 * only place that finds them: the gym's own lists and searches leave them out. One comes in at the
 * desk through the new-member form, by typing their phone.
 *
 * The search and the page live in the URL (`/site-users?q=4567&page=2`), as on the member list.
 */
export function SiteUsersPage() {
  const [params, setParams] = useSearchParams();
  const q = params.get("q") ?? "";
  const page = pageFromParams(params);

  // What is in the box; it runs ahead of `q` while the user types, and follows the URL when it
  // changes from outside (the back button), as on the member list.
  const [text, setText] = useState(q);
  const [shownQ, setShownQ] = useState(q);
  if (q !== shownQ) {
    setShownQ(q);
    setText(q);
  }

  const search = normalizeInput(q);
  const searching = search.length >= searchMinLength;
  const siteUsers = useSiteUserList({ search: searching ? search : undefined, page });

  function show(next: { q?: string; page?: number }, replace = false) {
    const nextQ = next.q ?? q;
    const nextPage = next.page ?? 1;
    const values: Record<string, string> = {};
    if (nextQ.trim() !== "") {
      values.q = nextQ;
    }
    if (nextPage > 1) {
      values.page = String(nextPage);
    }
    setParams(values, { replace });
  }

  const commit = (value: string) => show({ q: value }, true);
  const debouncedCommit = useDebouncedCallback(commit, searchDelayMs);

  return (
    <div className="space-y-6">
      <PageHero title="کاربران سایت" spreadOnPhone>
        <form
          role="search"
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
            placeholder="نام، ۴ رقم آخر یا شمارهٔ موبایل…"
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

      <Card edge>
        <CardHeader>
          <CardTitle>
            {searching ? "نتیجه جستجو" : "ثبت‌نام‌های سایت"}
            {siteUsers.isSuccess && (
              <span className="ms-2 text-sm font-normal text-muted-foreground">
                ({toPersianDigits(siteUsers.data.totalCount)})
              </span>
            )}
          </CardTitle>
          <p className="text-sm text-muted-foreground">
            کسانی که در سایت ثبت‌نام کرده‌اند و هنوز پلن یا ورودی ندارند. برای عضو کردن، شمارهٔ
            موبایلشان را در فرم «عضو جدید» وارد کنید.
          </p>
        </CardHeader>
        <CardContent className="space-y-4">
          {q.trim() !== "" && !searching && (
            <p className="text-sm text-muted-foreground">
              برای جستجو دست‌کم {toPersianDigits(searchMinLength)} حرف وارد کنید.
            </p>
          )}

          {siteUsers.isPending && <LoadingState />}
          {siteUsers.isError && (
            <Alert variant="destructive">{errorMessage(siteUsers.error)}</Alert>
          )}

          {siteUsers.isSuccess && siteUsers.data.items.length === 0 && (
            <EmptyState>
              {searching
                ? "کاربر سایتی با این مشخصات پیدا نشد."
                : "هنوز کسی در سایت ثبت‌نام نکرده است که عضو نشده باشد."}
            </EmptyState>
          )}

          {siteUsers.isSuccess && siteUsers.data.items.length > 0 && (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>نام</TableHead>
                    <TableHead>موبایل</TableHead>
                    <TableHead>تاریخ تولد</TableHead>
                    <TableHead>ثبت‌نام در سایت</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {siteUsers.data.items.map((siteUser) => (
                    <TableRow key={siteUser.id}>
                      <TableCell className="tone-row-edge font-medium">
                        {siteUser.fullName}
                      </TableCell>
                      <TableCell label="موبایل">
                        <span dir="ltr">{formatPhone(siteUser.phoneNumber)}</span>
                      </TableCell>
                      <TableCell label="تاریخ تولد">{formatDate(siteUser.birthDate)}</TableCell>
                      <TableCell label="ثبت‌نام در سایت">
                        {formatDateTime(siteUser.signedUpAt)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              <Pager
                page={page}
                pageCount={siteUsers.data.pageCount}
                onPageChange={(next) => show({ page: next })}
              />
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
