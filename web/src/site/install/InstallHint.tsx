import { Share, SquarePlus } from "lucide-react";
import { useState, useSyncExternalStore } from "react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

import { installPromptStore, isAppleMobile, isInstalledApp } from "./installPrompt";

/** When «بعداً» was pressed, on this phone and in this browser only. */
const dismissedKey = "gym-site-install-hint-dismissed-at";
const hiddenForMs = 30 * 24 * 60 * 60 * 1000;

function dismissedRecently(): boolean {
  try {
    const at = Number(localStorage.getItem(dismissedKey));
    return at > 0 && Date.now() - at < hiddenForMs;
  } catch {
    // Storage refused (a private window): the card shows, and «بعداً» hides it until a reload.
    return false;
  }
}

function rememberDismissed() {
  try {
    localStorage.setItem(dismissedKey, String(Date.now()));
  } catch {
    // As above.
  }
}

/**
 * Asks to install the app on the phone (roadmap 15.3b), on the sign-in screen and the home screen.
 * On Android the browser's offer becomes a «نصب برنامه» button; on an iPhone, which has no such
 * offer, the steps of Safari's Share menu. Hidden once the app runs from its icon, when the
 * browser makes no offer, and for 30 days after «بعداً».
 *
 * Shown before signing in on purpose: an installed iPhone app keeps its own sign-in, apart from
 * Safari's, so installing first saves a second code SMS.
 */
export function InstallHint() {
  const offer = useSyncExternalStore(installPromptStore.subscribe, installPromptStore.get);
  const [dismissed, setDismissed] = useState(dismissedRecently);

  const apple = isAppleMobile();

  if (dismissed || isInstalledApp() || (!apple && offer === null)) {
    return null;
  }

  const dismiss = () => {
    rememberDismissed();
    setDismissed(true);
  };

  return (
    <Card accent={false} className="shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          نصب برنامه روی گوشی
        </CardTitle>
        <CardDescription>
          باشگاه پاسارگاد را مثل یک برنامه روی صفحهٔ گوشی‌تان داشته باشید.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {apple ? (
          <ol className="list-inside list-decimal space-y-2 text-sm">
            <li>
              در Safari دکمهٔ اشتراک‌گذاری <Share aria-label="Share" className="inline size-4" /> را
              بزنید.
            </li>
            <li>
              <span dir="ltr">«Add to Home Screen»</span>{" "}
              <SquarePlus aria-hidden className="inline size-4" /> و بعد{" "}
              <span dir="ltr">«Add»</span> را بزنید.
            </li>
            <li>برنامه را از آیکون آن باز کنید و همان‌جا وارد شوید.</li>
          </ol>
        ) : (
          <Button className="w-full" onClick={() => void installPromptStore.install()}>
            نصب برنامه
          </Button>
        )}
        <Button variant="ghost" className="w-full" onClick={dismiss}>
          بعداً
        </Button>
      </CardContent>
    </Card>
  );
}
