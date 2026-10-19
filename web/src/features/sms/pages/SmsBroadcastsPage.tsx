import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

import { BroadcastForm, type BroadcastNotice } from "../components/BroadcastForm";
import { BroadcastList } from "../components/BroadcastList";
import { SmsCreditLine } from "../components/SmsCreditLine";

/**
 * «پیامک گروهی» (BUSINESS_RULES.md §10 *The broadcast*, task 10.13): the Owner writes a text for a
 * group of members and sends it at once, after a confirmation with its cost; below, the broadcasts
 * sent before. Owner only: the route is behind RequireRole and the API refuses staff anyway.
 */
export function SmsBroadcastsPage() {
  const [notice, setNotice] = useState<BroadcastNotice | null>(null);

  return (
    <div className="space-y-4">
      <h2 className="text-xl font-bold">پیامک گروهی</h2>

      <SmsCreditLine />

      <Card>
        <CardHeader>
          <CardTitle>پیامک تازه</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {notice !== null && (
            <Alert variant={notice.tone === "success" ? "success" : "destructive"} role="status">
              {notice.text}
            </Alert>
          )}
          <BroadcastForm onSent={setNotice} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>پیامک‌های گروهی فرستاده‌شده</CardTitle>
        </CardHeader>
        <CardContent>
          <BroadcastList />
        </CardContent>
      </Card>
    </div>
  );
}
