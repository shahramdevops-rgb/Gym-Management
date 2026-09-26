import { CheckCircle2 } from "lucide-react";
import type { ReactNode } from "react";

import { Button } from "@/components/ui/button";
import { DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";

interface DialogSuccessProps {
  title: string;
  description?: string;
  /** What was saved, when the desk should be able to check it (the cafe lists the items). */
  children?: ReactNode;
  onClose: () => void;
}

/**
 * The last step of a dialog whose form went through: a green "✓ … ثبت شد", what was saved, and a
 * close button. A dialog that just disappears leaves the desk guessing whether anything happened.
 *
 * The close button takes the focus, which moves it off the form that is gone and lets Enter close
 * the box — the next member is usually already waiting.
 */
export function DialogSuccess({ title, description, children, onClose }: DialogSuccessProps) {
  return (
    <>
      <DialogHeader>
        <DialogTitle className="flex items-center gap-2 text-success">
          <CheckCircle2 className="size-5" aria-hidden />
          {title}
        </DialogTitle>
        {description !== undefined && <DialogDescription>{description}</DialogDescription>}
      </DialogHeader>
      {children}
      <div className="flex">
        <Button variant="outline" autoFocus onClick={onClose}>
          بستن
        </Button>
      </div>
    </>
  );
}
