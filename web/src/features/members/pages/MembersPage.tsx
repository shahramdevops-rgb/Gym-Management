import { UserPlus } from "lucide-react";
import { Link, useSearchParams } from "react-router";

import { paths } from "@/app/paths";
import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";
import { pageFromParams } from "@/lib/searchParams";

import { useMemberList } from "../api";
import { MembersTable } from "../components/MembersTable";

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
 * Every member, by name, a page at a time. Inactive members are included by default
 * (docs/BUSINESS_RULES.md §2), so staff can find someone to reactivate. The filter and the page
 * are in the URL (`/members?status=inactive&page=2`), like the search on the home page.
 */
export function MembersPage() {
  const [params, setParams] = useSearchParams();
  const status = statusFromParams(params);
  const page = pageFromParams(params);

  const members = useMemberList({
    isActive: status === "all" ? undefined : status === "active",
    page,
  });

  function show(nextStatus: StatusFilter, nextPage = 1) {
    const next: Record<string, string> = {};
    if (nextStatus !== "all") {
      next.status = nextStatus;
    }
    if (nextPage > 1) {
      next.page = String(nextPage);
    }
    setParams(next);
  }

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

      <Card>
        <CardHeader className="flex flex-wrap items-center justify-between gap-3">
          <CardTitle>
            فهرست اعضا
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
                onClick={() => show(filter.value)}
              >
                {filter.label}
              </Button>
            ))}
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {members.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
          {members.isError && <Alert variant="destructive">{errorMessage(members.error)}</Alert>}

          {members.isSuccess && members.data.items.length === 0 && (
            <p className="text-muted-foreground">
              {status === "all" ? "هنوز هیچ عضوی ثبت نشده است." : "عضوی با این وضعیت نیست."}
            </p>
          )}

          {members.isSuccess && members.data.items.length > 0 && (
            <>
              <MembersTable members={members.data.items} />
              <Pager
                page={page}
                pageCount={members.data.pageCount}
                onPageChange={(next) => show(status, next)}
              />
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
