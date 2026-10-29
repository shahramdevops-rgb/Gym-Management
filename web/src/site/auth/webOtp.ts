/**
 * Android Chrome's WebOTP API: with the person's tap on «اجازه», it reads the code from the SMS
 * and the code box fills itself (asked by the developer, BUSINESS_RULES.md §15). It works only
 * when the SMS's last line is `@app.pasargadgymplus.ir #12345`, the site's own address; the
 * template `gymExpiring` does not have that line yet, and until it does this simply never
 * answers. iPhones need none of this: the box's `autocomplete="one-time-code"` is enough.
 *
 * Resolves to the code, or to undefined when the browser has no WebOTP, the person declined, or
 * `signal` stopped the wait (the screen was left, or the code was typed by hand first).
 */
export async function readCodeFromSms(signal: AbortSignal): Promise<string | undefined> {
  if (!("OTPCredential" in window)) {
    return undefined;
  }

  try {
    // `otp` is not in TypeScript's DOM types yet.
    const credential = (await navigator.credentials.get({
      otp: { transport: ["sms"] },
      signal,
    } as CredentialRequestOptions)) as (Credential & { code?: string }) | null;

    return credential?.code;
  } catch {
    return undefined;
  }
}
