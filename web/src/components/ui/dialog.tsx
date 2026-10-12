import * as React from "react";
import * as DialogPrimitive from "@radix-ui/react-dialog";
import { X } from "lucide-react";

import { cn } from "@/lib/utils";

/**
 * A modal dialog. Radix handles what is easy to get wrong by hand: focus moves into the dialog
 * and is trapped there, Escape and the overlay close it, and the rest of the page is hidden from
 * screen readers while it is open.
 *
 * This exists so a form can open *over* a table row instead of inside it. A form rendered in a
 * table cell makes the row grow, which is how the front desk board ended up with rows three lines
 * tall (task 6.5.2).
 */
const Dialog = DialogPrimitive.Root;
const DialogTrigger = DialogPrimitive.Trigger;
const DialogClose = DialogPrimitive.Close;

/**
 * A form's box on a phone (13.1c): the whole screen, so nothing of the page behind is left to
 * tap by mistake and the keyboard has room. A flex column rather than the card's grid, so a row
 * of `dialogActionsClassName` can sit at the bottom when the form is short. Classes only below
 * `lg`; the computer keeps the centred card, and `!` wins over a width a dialog sets for the
 * computer (`sm:max-w-3xl` would otherwise come later in the stylesheet). No translate at all,
 * not even a zero one, which would make the box the frame of the ✕'s `fixed` below.
 */
const fullScreenOnPhoneClassName = cn(
  "max-lg:start-0 max-lg:top-0 max-lg:flex max-lg:h-dvh max-lg:max-h-dvh max-lg:w-full max-lg:max-w-none!",
  "max-lg:translate-none max-lg:flex-col max-lg:rounded-none max-lg:border-0 max-lg:p-4",
);

function DialogContent({
  className,
  children,
  fullScreenOnPhone = false,
  ...props
}: React.ComponentProps<typeof DialogPrimitive.Content> & {
  /** A form fills a phone's screen; a short confirmation stays a card (decided 13.1c). */
  fullScreenOnPhone?: boolean;
}) {
  return (
    <DialogPrimitive.Portal>
      <DialogPrimitive.Overlay
        data-slot="dialog-overlay"
        className="fixed inset-0 z-50 bg-black/40 backdrop-blur-[2px] motion-safe:data-[state=open]:animate-overlay-in"
      />
      <DialogPrimitive.Content
        data-slot="dialog-content"
        data-phone-full={fullScreenOnPhone ? "" : undefined}
        className={cn(
          // Full width on a phone with a gutter, a centred card from sm up. The form inside can be
          // taller than the screen, so the content scrolls rather than overflowing the viewport.
          "fixed start-1/2 top-1/2 z-50 grid max-h-[calc(100dvh-2rem)] w-[calc(100vw-2rem)] max-w-md",
          "translate-x-1/2 -translate-y-1/2 gap-4 overflow-y-auto rounded-2xl border bg-card p-6 shadow-overlay motion-safe:data-[state=open]:animate-dialog-in",
          className,
          fullScreenOnPhone && fullScreenOnPhoneClassName,
        )}
        {...props}
      >
        {children}
        <DialogPrimitive.Close
          className={cn(
            "absolute end-4 top-4 rounded-md p-1 text-muted-foreground transition-colors hover:bg-accent hover:text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-ring",
            // A finger-sized target on a phone (40px), the icon where it was. In a box that fills
            // the screen it stays in the corner while the form scrolls, on the box's colour.
            "max-lg:end-1 max-lg:top-1 max-lg:p-3",
            "max-lg:in-data-phone-full:fixed max-lg:in-data-phone-full:z-20 max-lg:in-data-phone-full:bg-card",
          )}
          aria-label="بستن"
        >
          <X className="size-4" />
        </DialogPrimitive.Close>
      </DialogPrimitive.Content>
    </DialogPrimitive.Portal>
  );
}

function DialogHeader({ className, ...props }: React.ComponentProps<"div">) {
  return <div data-slot="dialog-header" className={cn("grid gap-1.5", className)} {...props} />;
}

function DialogTitle({ className, ...props }: React.ComponentProps<typeof DialogPrimitive.Title>) {
  return (
    <DialogPrimitive.Title
      data-slot="dialog-title"
      className={cn("text-lg font-bold", className)}
      {...props}
    />
  );
}

function DialogDescription({
  className,
  ...props
}: React.ComponentProps<typeof DialogPrimitive.Description>) {
  return (
    <DialogPrimitive.Description
      data-slot="dialog-description"
      className={cn("text-sm text-muted-foreground", className)}
      {...props}
    />
  );
}

export {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
};
