import { Search, X } from "lucide-react";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useMember, useMemberList } from "@/features/members/api";
import { searchMinLength } from "@/features/members/schemas";
import { errorMessage } from "@/lib/errors";
import { formatPhone, toPersianDigits } from "@/lib/format";
import { normalizeInput } from "@/lib/normalize";
import { useDebouncedCallback } from "@/lib/useDebouncedCallback";

const searchDelayMs = 300;

/** How many matches are offered; the member list is where a long list belongs. */
const shownMatches = 5;

interface MemberFilterProps {
  /** The chosen member's id, from the URL, or undefined for everyone. */
  memberId: string | undefined;
  onChange: (memberId: string | undefined) => void;
}

/**
 * "Whose history": everyone, or one member found by name or mobile the way the cafe's till finds
 * one (BUSINESS_RULES.md §12 History). Only the id is kept in the URL, so the chosen member's name
 * is read back from the API after a reload.
 */
export function MemberFilter({ memberId, onChange }: MemberFilterProps) {
  const [text, setText] = useState("");
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedCallback(
    (value: string) => setSearch(normalizeInput(value)),
    searchDelayMs,
  );

  const chosen = useMember(memberId ?? "", { enabled: memberId !== undefined });
  const ready = search.length >= searchMinLength;
  const results = useMemberList({ search, page: 1 }, { enabled: ready && memberId === undefined });

  if (memberId !== undefined) {
    return (
      <div className="space-y-2">
        <Label>عضو</Label>
        <div className="flex h-9 items-center justify-between gap-3 rounded-md border px-3">
          <span className="truncate font-medium">
            {chosen.isSuccess ? chosen.data.fullName : chosen.isError ? "عضو پیدا نشد" : "…"}
          </span>
          <Button
            size="sm"
            variant="ghost"
            onClick={() => {
              setText("");
              setSearch("");
              onChange(undefined);
            }}
          >
            <X aria-hidden />
            همهٔ اعضا
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="relative space-y-2">
      <Label htmlFor="history-member">عضو</Label>
      <div className="relative">
        <Search
          className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
          aria-hidden
        />
        <Input
          id="history-member"
          type="search"
          placeholder="همهٔ اعضا — یا نام یا موبایل…"
          className="ps-9"
          value={text}
          onChange={(event) => {
            setText(event.target.value);
            debouncedSearch.run(event.target.value);
          }}
        />
      </div>

      {text.trim() !== "" && !ready && (
        <p className="text-sm text-muted-foreground">
          دست‌کم {toPersianDigits(searchMinLength)} حرف وارد کنید.
        </p>
      )}
      {ready && results.isPending && <p className="text-sm text-muted-foreground">در حال جستجو…</p>}
      {ready && results.isError && (
        <p className="text-sm text-destructive">{errorMessage(results.error)}</p>
      )}
      {ready && results.isSuccess && results.data.items.length === 0 && (
        <p className="text-sm text-muted-foreground">عضوی با این مشخصات پیدا نشد.</p>
      )}
      {ready && results.isSuccess && results.data.items.length > 0 && (
        <ul
          className="absolute inset-x-0 z-10 divide-y rounded-md border bg-card shadow-md"
          aria-label="اعضای پیدا شده"
        >
          {results.data.items.slice(0, shownMatches).map((found) => (
            <li key={found.id}>
              <button
                type="button"
                className="flex w-full items-center justify-between gap-3 px-3 py-2 text-start hover:bg-accent"
                onClick={() => onChange(found.id)}
              >
                <span>{found.fullName}</span>
                <span className="text-sm text-muted-foreground" dir="ltr">
                  {formatPhone(found.phoneNumber)}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
