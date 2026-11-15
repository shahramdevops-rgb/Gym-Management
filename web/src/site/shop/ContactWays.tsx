import { formatPhone, toPersianDigits } from "@/lib/format";

import type { ShowcaseContacts } from "./api";

interface Way {
  label: string;
  /** What is shown: the number as people write it, or the id. */
  text: string;
  href: string;
  /** A phone number is dialled in place; a messenger opens beside the shop. */
  external: boolean;
}

/** `+989121234567` → `989121234567`, as wa.me wants it. */
const withoutPlus = (phone: string) => phone.replace(/^\+/, "");

/**
 * The shop's ways, in the order the shop admin's «راه‌های تماس» page lists them, each with the
 * address that opens it. One the shop admin left empty is not shown (BUSINESS_RULES.md §16 *The
 * shop admin*). The labels are the site's own copies, not the panel's.
 */
function waysOf(contacts: ShowcaseContacts): Way[] {
  const handle = (label: string, id: string | null, address: string): Way | null =>
    id === null ? null : { label, text: `@${id}`, href: `${address}${id}`, external: true };

  const ways: (Way | null)[] = [
    contacts.mobilePhone === null
      ? null
      : {
          label: "تلفن همراه",
          text: formatPhone(contacts.mobilePhone),
          href: `tel:${contacts.mobilePhone}`,
          external: false,
        },
    contacts.landlinePhone === null
      ? null
      : {
          label: "تلفن ثابت",
          text: toPersianDigits(contacts.landlinePhone),
          href: `tel:${contacts.landlinePhone}`,
          external: false,
        },
    contacts.whatsAppPhone === null
      ? null
      : {
          label: "واتساپ",
          text: formatPhone(contacts.whatsAppPhone),
          href: `https://wa.me/${withoutPlus(contacts.whatsAppPhone)}`,
          external: true,
        },
    handle("تلگرام", contacts.telegram, "https://t.me/"),
    handle("روبیکا", contacts.rubika, "https://rubika.ir/"),
    handle("ایتا", contacts.eitaa, "https://eitaa.com/"),
    handle("بله", contacts.bale, "https://ble.ir/"),
    handle("اینستاگرام", contacts.instagram, "https://instagram.com/"),
  ];

  return ways.filter((way) => way !== null);
}

/**
 * «راه‌های تماس با فروشگاه», at the foot of the showcase and of every product's page
 * (BUSINESS_RULES.md §16 *The showcase*): until buying opens on the site, this is how a customer
 * orders. Small boxes side by side, each a link. Nothing at all when the shop gave no way.
 */
export function ContactWays({ contacts }: { contacts: ShowcaseContacts }) {
  const ways = waysOf(contacts);
  if (ways.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="shop-contact-title" className="space-y-4">
      <div className="space-y-1">
        <h2 id="shop-contact-title" className="text-lg font-bold">
          راه‌های تماس با فروشگاه
        </h2>
        <p className="text-sm text-muted-foreground">
          برای سفارش یا پرسش دربارهٔ کالاها با فروشگاه در تماس باشید.
        </p>
      </div>

      <ul className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4">
        {ways.map((way) => (
          <li key={way.label}>
            <a
              href={way.href}
              {...(way.external ? { target: "_blank", rel: "noopener noreferrer" } : {})}
              className="block h-full rounded-md border bg-card px-3 py-2 transition-colors hover:border-primary focus-visible:outline-2 focus-visible:outline-ring"
            >
              <span className="block text-xs text-muted-foreground">{way.label}</span>
              {/* Left to right: a number's groups and an id read in their own order. */}
              <span dir="ltr" className="block truncate text-end text-sm font-medium">
                {way.text}
              </span>
            </a>
          </li>
        ))}
      </ul>
    </section>
  );
}
