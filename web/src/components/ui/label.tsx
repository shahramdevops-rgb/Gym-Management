import * as React from "react";

import { cn } from "@/lib/utils";

/**
 * shadcn/ui's Label is built on @radix-ui/react-label, which is not on the approved package
 * list. A native <label> gives the same behaviour for the forms in this app.
 */
function Label({ className, ...props }: React.ComponentProps<"label">) {
  return (
    <label
      data-slot="label"
      className={cn(
        "flex items-center gap-2 text-sm leading-none font-medium select-none",
        className,
      )}
      {...props}
    />
  );
}

export { Label };
