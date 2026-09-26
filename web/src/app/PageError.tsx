import { useRouteError } from "react-router";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";

/**
 * What a page shows when it fails while rendering. Without it React Router replaces the whole
 * app with its own English error screen, so one broken page took the menu and every other page
 * down with it (found in task 7.4, when an API older than the frontend sent a board row without
 * a field the page read).
 *
 * It sits inside the frame, so the menu stays and the desk can go on working elsewhere. The
 * technical message is logged for the developer, not shown: it means nothing at the front desk.
 */
export function PageError() {
  const error = useRouteError();
  console.error(error);

  return (
    <div className="max-w-xl space-y-4">
      <Alert variant="destructive" role="alert">
        نمایش این صفحه با خطا روبه‌رو شد. صفحه را دوباره بارگذاری کنید؛ اگر خطا تکرار شد، به مدیر
        سیستم اطلاع دهید.
      </Alert>
      <Button variant="outline" onClick={() => window.location.reload()}>
        بارگذاری دوباره
      </Button>
    </div>
  );
}
