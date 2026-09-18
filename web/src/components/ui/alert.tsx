import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";

import { cn } from "@/lib/utils";

const alertVariants = cva("w-full rounded-lg border px-4 py-3 text-sm", {
  variants: {
    variant: {
      default: "bg-card text-card-foreground",
      destructive: "border-destructive/50 bg-destructive/5 text-destructive",
      success: "border-success/50 bg-success/5 text-success",
    },
  },
  defaultVariants: {
    variant: "default",
  },
});

/** A message box. `role` defaults to "alert" so screen readers announce it when it appears. */
function Alert({
  className,
  variant,
  role = "alert",
  ...props
}: React.ComponentProps<"div"> & VariantProps<typeof alertVariants>) {
  return (
    <div
      data-slot="alert"
      role={role}
      className={cn(alertVariants({ variant }), className)}
      {...props}
    />
  );
}

export { Alert };
