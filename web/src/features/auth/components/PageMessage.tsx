import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

import { LoadingState } from "@/components/States";

/**
 * A short centred message in place of a page, such as "not allowed": an icon in a circle and the
 * words under it, the shape of `EmptyState` without its box (roadmap 13.2g). The circle is in the
 * page's colour, grey outside the frame. The icon is decoration: the words say it all.
 */
export function PageMessage({ icon: Icon, children }: { icon: LucideIcon; children: ReactNode }) {
  return (
    <div className="flex min-h-[50vh] flex-col items-center justify-center gap-3 p-6 text-center">
      <span className="flex size-12 items-center justify-center rounded-full tone-soft tone-ink">
        <Icon className="size-6" aria-hidden />
      </span>
      <p className="text-muted-foreground">{children}</p>
    </div>
  );
}

/**
 * In place of a page while the app is still finding out what to show: the session being restored,
 * a page's file downloading, the user's role being read. The same turning circle as every list's
 * `LoadingState`, centred where the page will be.
 */
export function PageLoading() {
  return (
    <div className="flex min-h-[50vh] items-center justify-center p-6">
      <LoadingState />
    </div>
  );
}
