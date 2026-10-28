import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { networkErrorMessage } from "@/lib/errors";

import { retrySiteSession } from "../api/siteAuthFetch";
import { SiteFrame } from "./SiteFrame";

/**
 * The server gave no answer it could trust (BUSINESS_RULES.md §15): the person stays signed in and
 * tries again, rather than being sent to ask for a new code, which is a paid SMS.
 */
export function UnreachableNotice() {
  const [retrying, setRetrying] = useState(false);

  const retry = async () => {
    setRetrying(true);
    try {
      await retrySiteSession();
    } finally {
      setRetrying(false);
    }
  };

  return (
    <SiteFrame>
      <Alert variant="destructive">{networkErrorMessage}</Alert>
      <Button className="w-full" disabled={retrying} onClick={() => void retry()}>
        {retrying ? "در حال تلاش…" : "دوباره امتحان کنید"}
      </Button>
    </SiteFrame>
  );
}
