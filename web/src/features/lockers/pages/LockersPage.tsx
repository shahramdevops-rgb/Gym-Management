import { useState } from "react";
import { useSearchParams } from "react-router";

import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { errorMessage } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";
import { normalizeDigits } from "@/lib/normalize";
import { pageFromParams } from "@/lib/searchParams";

import { useCreateLocker, useLockerList, useSetLockerOutOfService, type Locker } from "../api";
import { LockersTable } from "../components/LockersTable";

/** A positive whole number typed with any digits, or null for anything else. */
function parseLockerNumber(text: string): number | null {
  const normalized = normalizeDigits(text).trim();

  return /^[1-9]\d*$/.test(normalized) ? Number(normalized) : null;
}

/**
 * Owner only: the gym's lockers, with live occupancy (derived from an open attendance,
 * BUSINESS_RULES.md §6) and a one-field form to add another. Check-in picks a locker itself
 * (task 5.2), so this screen is only setup, never a browsing step for the front desk.
 */
export function LockersPage() {
  const [params, setParams] = useSearchParams();
  const page = pageFromParams(params);
  const lockers = useLockerList(page);
  const createLocker = useCreateLocker();
  const setOutOfService = useSetLockerOutOfService();
  const [numberText, setNumberText] = useState("");
  const [notice, setNotice] = useState<{ kind: "success" | "destructive"; text: string } | null>(
    null,
  );

  async function addLocker(event: React.FormEvent) {
    event.preventDefault();
    setNotice(null);
    const number = parseLockerNumber(numberText);
    if (number === null) {
      setNotice({ kind: "destructive", text: "شماره کمد باید عددی مثبت باشد." });
      return;
    }

    try {
      await createLocker.mutateAsync(number);
      setNumberText("");
      setNotice({ kind: "success", text: `کمد شماره ${toPersianDigits(number)} اضافه شد.` });
    } catch (problem) {
      setNotice({ kind: "destructive", text: errorMessage(problem) });
    }
  }

  async function toggleOutOfService(locker: Locker) {
    setNotice(null);
    try {
      await setOutOfService.mutateAsync({ id: locker.id, outOfService: !locker.isOutOfService });
    } catch (problem) {
      setNotice({ kind: "destructive", text: errorMessage(problem) });
    }
  }

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-bold">کمدها</h2>

      <Card>
        <CardHeader>
          <CardTitle>کمد جدید</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={(event) => void addLocker(event)} className="flex items-end gap-3">
            <div className="grid gap-1.5">
              <Label htmlFor="locker-number">شماره کمد</Label>
              <Input
                id="locker-number"
                inputMode="numeric"
                className="w-32"
                value={numberText}
                onChange={(event) => setNumberText(event.target.value)}
              />
            </div>
            <Button type="submit" disabled={createLocker.isPending}>
              {createLocker.isPending ? "در حال ثبت…" : "افزودن"}
            </Button>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>
            فهرست کمدها
            {lockers.isSuccess && (
              <span className="ms-2 text-sm font-normal text-muted-foreground">
                ({toPersianDigits(lockers.data.totalCount)})
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {notice !== null && (
            <Alert variant={notice.kind} role={notice.kind === "success" ? "status" : "alert"}>
              {notice.text}
            </Alert>
          )}

          {lockers.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
          {lockers.isError && <Alert variant="destructive">{errorMessage(lockers.error)}</Alert>}

          {lockers.isSuccess && lockers.data.items.length === 0 && (
            <p className="text-muted-foreground">هنوز هیچ کمدی تعریف نشده است.</p>
          )}

          {lockers.isSuccess && lockers.data.items.length > 0 && (
            <>
              <LockersTable
                lockers={lockers.data.items}
                busy={setOutOfService.isPending}
                onToggleOutOfService={(locker) => void toggleOutOfService(locker)}
              />
              <Pager
                page={page}
                pageCount={lockers.data.pageCount}
                onPageChange={(next) => setParams({ page: String(next) })}
              />
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
