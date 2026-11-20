import { Phone } from "lucide-react";
import type { ReactNode } from "react";
import { Link } from "react-router";

import { Badge } from "@/components/ui/badge";

/**
 * A member as the trainer sees them (BUSINESS_RULES.md §15 *A member and their trainer*): name, a
 * call icon, and one line of detail. The number itself shows only on the student's profile (the
 * developer, 1405/08/29, 15.7p): in a list the icon calls them. Given `to`, the name opens that
 * page: a student's profile (15.7p), underlined faintly so a tap on it is expected.
 */
export function Person({
  fullName,
  phoneNumber,
  detail,
  inactive = false,
  to,
}: {
  fullName: string;
  phoneNumber: string;
  detail?: ReactNode;
  inactive?: boolean;
  to?: string;
}) {
  return (
    <div className="space-y-0.5">
      <p className="flex flex-wrap items-center gap-x-2 font-semibold">
        {to === undefined ? (
          fullName
        ) : (
          <Link
            to={to}
            className="underline decoration-muted-foreground/40 underline-offset-4 transition-colors hover:decoration-current focus-visible:rounded-sm focus-visible:ring-[3px] focus-visible:ring-ring/30 focus-visible:outline-none"
          >
            {fullName}
          </Link>
        )}
        <CallButton fullName={fullName} phoneNumber={phoneNumber} />
        {inactive && <Badge variant="secondary">غیرفعال</Badge>}
      </p>
      {detail !== undefined && <p className="text-sm text-muted-foreground">{detail}</p>}
    </div>
  );
}

/** A round call icon: a tap opens the phone's dialler on the member's number. */
export function CallButton({ fullName, phoneNumber }: { fullName: string; phoneNumber: string }) {
  return (
    <a
      href={`tel:${phoneNumber}`}
      aria-label={`تماس با ${fullName}`}
      title={`تماس با ${fullName}`}
      className="inline-flex size-8 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary transition-colors hover:bg-primary/20 focus-visible:ring-[3px] focus-visible:ring-ring/30 focus-visible:outline-none"
    >
      <Phone aria-hidden className="size-4" />
    </a>
  );
}
