import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { CircleAlert, CircleCheck } from "lucide-react";

import { cn } from "@/lib/utils";

const alertVariants = cva(
  "flex w-full items-start gap-3 rounded-xl border px-4 py-3 text-sm [&>svg]:mt-1 [&>svg]:size-4 [&>svg]:shrink-0",
  {
    variants: {
      variant: {
        default: "bg-card text-card-foreground",
        destructive: "border-destructive/30 bg-destructive/8 text-destructive",
        success: "border-success/30 bg-success/8 text-success",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  },
);

/** The icon that says at a glance what kind of message it is; colour is never the only sign. */
const icons = {
  destructive: CircleAlert,
  success: CircleCheck,
} as const;

/** A message box. `role` defaults to "alert" so screen readers announce it when it appears. */
function Alert({
  className,
  variant,
  role = "alert",
  children,
  ...props
}: React.ComponentProps<"div"> & VariantProps<typeof alertVariants>) {
  const Icon = variant === "destructive" || variant === "success" ? icons[variant] : null;

  return (
    <div
      data-slot="alert"
      role={role}
      className={cn(alertVariants({ variant }), className)}
      {...props}
    >
      {Icon !== null && <Icon aria-hidden />}
      <div className="min-w-0 flex-1">{children}</div>
    </div>
  );
}

export { Alert };
