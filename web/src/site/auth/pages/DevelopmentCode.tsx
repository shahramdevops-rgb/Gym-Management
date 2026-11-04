import { useEffect, useState } from "react";

import { toPersianDigits } from "@/lib/format";

import type { SentCode } from "./SignInPage";

/**
 * The sign-in code at the top of the screen, on the developer's own computer only (asked by the
 * developer, 1405/08/09), so the site app can be tried without a phone. Rendered only in a
 * development build (`import.meta.env.DEV`), and the API answers only in its Development
 * environment and only to this computer: a production build carries none of it, and the server has
 * no such path.
 *
 * Plain `fetch`, not the typed client: the path exists only on a development API.
 */
export function DevelopmentCode({ sent }: { sent: SentCode }) {
  const [code, setCode] = useState<string | null>(null);

  useEffect(() => {
    let current = true;
    // Read again after «ارسال دوباره» too: a new SentCode, and a new code once the old one expired.
    void fetch(
      `/api/site/auth/development-code?phoneNumber=${encodeURIComponent(sent.phoneNumber)}`,
    )
      .then((response) => (response.ok ? (response.json() as Promise<{ code: string }>) : null))
      .then((body) => current && setCode(body?.code ?? null))
      .catch(() => current && setCode(null));

    return () => {
      current = false;
    };
  }, [sent]);

  if (code === null) {
    return null;
  }

  return (
    <div
      role="status"
      className="fixed inset-x-0 top-0 z-50 bg-amber-400 px-4 py-2 text-center text-sm font-semibold text-amber-950"
    >
      فقط روی کامپیوتر خودتان — کد ورود: <span dir="ltr">{toPersianDigits(code)}</span>
    </div>
  );
}
