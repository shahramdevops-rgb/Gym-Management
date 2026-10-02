import { Search, X } from "lucide-react";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { CurrentlyInside } from "@/features/attendance/api";
import { guestLabel } from "@/features/attendance/holder";
import { nameMatches, nameSearchTerm } from "@/features/lockers/nameSearch";
import { useMemberList, type Member } from "@/features/members/api";
import { searchMinLength } from "@/features/members/schemas";
import { errorMessage } from "@/lib/errors";
import { formatPhone, toPersianDigits } from "@/lib/format";
import { normalizeInput } from "@/lib/normalize";
import { useDebouncedCallback } from "@/lib/useDebouncedCallback";

const searchDelayMs = 300;

/** How many matches the till offers; the member search page is where a long list belongs. */
const shownMatches = 5;

interface CafeMemberPickerProps {
  /** The member the order is for, or null for a walk-in customer. */
  member: Pick<Member, "id" | "fullName" | "phoneNumber"> | null;
  onChange: (member: Member | null) => void;
  /** The guests inside now, who can be chosen as the buyer (BUSINESS_RULES.md §7 *Guest visit*, §8). */
  guests?: CurrentlyInside[];
  /** The guest the order is for, if one was chosen. */
  guest?: CurrentlyInside | null;
  onGuestChange?: (guest: CurrentlyInside | null) => void;
}

/**
 * Who is buying: nobody in particular (آزاد, a walk-in), or a member found by name or phone the
 * same way the home page finds one. Naming a member is what lets the order go on their account
 * (BUSINESS_RULES.md §8), so the choice is always visible above the payment.
 *
 * A guest who is inside can be chosen too (§7 *Guest visit*): they are listed under the search,
 * narrowed by what is typed, and their order goes on their visit under their name.
 *
 * An inactive member is offered too: §2 stops them coming in and taking a new subscription, not
 * buying a bottle of water (decided in task 7.2).
 */
export function CafeMemberPicker({
  member,
  onChange,
  guests = [],
  guest = null,
  onGuestChange,
}: CafeMemberPickerProps) {
  const [text, setText] = useState("");
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedCallback(
    (value: string) => setSearch(normalizeInput(value)),
    searchDelayMs,
  );

  const ready = search.length >= searchMinLength;
  const results = useMemberList(
    { search, page: 1 },
    { enabled: ready && member === null && guest === null },
  );
  // The map's name search: the same normalization, and nothing narrowed below its minimum.
  const term = nameSearchTerm(text);
  const shownGuests =
    term === null ? guests : guests.filter((inside) => nameMatches(inside.guestName ?? "", term));

  if (guest !== null) {
    return (
      <div className="flex items-center justify-between gap-3 rounded-md border px-3 py-2">
        <div>
          <p className="font-medium">{guest.guestName}</p>
          <p className="text-sm text-muted-foreground">
            {guestLabel} · {guestPlace(guest)}
          </p>
        </div>
        <Button
          size="sm"
          variant="ghost"
          onClick={() => {
            setText("");
            setSearch("");
            onGuestChange?.(null);
          }}
        >
          <X aria-hidden />
          مشتری آزاد
        </Button>
      </div>
    );
  }

  if (member !== null) {
    return (
      <div className="flex items-center justify-between gap-3 rounded-md border px-3 py-2">
        <div>
          <p className="font-medium">{member.fullName}</p>
          <p className="text-sm text-muted-foreground" dir="ltr">
            {formatPhone(member.phoneNumber)}
          </p>
        </div>
        <Button
          size="sm"
          variant="ghost"
          onClick={() => {
            setText("");
            setSearch("");
            onChange(null);
          }}
        >
          <X aria-hidden />
          مشتری آزاد
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <div className="relative">
        <Search
          className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
          aria-hidden
        />
        <Input
          type="search"
          aria-label="جستجوی عضو"
          placeholder="مشتری آزاد — یا نام یا موبایل عضو…"
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
        <ul className="divide-y rounded-md border" aria-label="اعضای پیدا شده">
          {results.data.items.slice(0, shownMatches).map((found) => (
            <li key={found.id}>
              <button
                type="button"
                className="flex w-full items-center justify-between gap-3 px-3 py-2 text-start hover:bg-accent"
                onClick={() => onChange(found)}
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

      {shownGuests.length > 0 && onGuestChange !== undefined && (
        <section aria-label="مهمان‌های داخل باشگاه" className="space-y-1">
          <p className="text-xs text-muted-foreground">مهمان‌های داخل باشگاه</p>
          <ul className="divide-y rounded-md border">
            {shownGuests.map((inside) => (
              <li key={inside.attendanceId}>
                <button
                  type="button"
                  className="flex w-full items-center justify-between gap-3 px-3 py-2 text-start hover:bg-accent"
                  onClick={() => onGuestChange(inside)}
                >
                  <span>{inside.guestName}</span>
                  <span className="text-sm text-muted-foreground">
                    {guestLabel} · {guestPlace(inside)}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}

/** "کمد ۳", or that the guest is on a reserve place. */
function guestPlace(inside: CurrentlyInside): string {
  return inside.lockerNumber === null ? "بدون کمد" : `کمد ${toPersianDigits(inside.lockerNumber)}`;
}
