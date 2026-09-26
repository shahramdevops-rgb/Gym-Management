import { Minus, Plus, Trash2 } from "lucide-react";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { formatMoney, toPersianDigits } from "@/lib/format";

import { cafeLimits } from "../api";
import { lineTotal, parseQuantity, type CartLine } from "../cart";

interface CartLinesProps {
  cart: CartLine[];
  onQuantityChange: (productId: string, quantity: number) => void;
  onRemove: (productId: string) => void;
}

/** The cart's lines: a quantity that can be stepped or typed, the line total, and a way out. */
export function CartLines({ cart, onQuantityChange, onRemove }: CartLinesProps) {
  if (cart.length === 0) {
    return <p className="text-muted-foreground">سبد خالی است. روی محصول‌ها بزنید.</p>;
  }

  return (
    <ul className="divide-y" aria-label="سبد خرید">
      {cart.map((line) => (
        <li key={line.productId} className="space-y-2 py-2">
          <div className="flex items-start justify-between gap-2">
            <span className="font-medium">{line.name}</span>
            <span className="text-sm">{formatMoney(lineTotal(line))}</span>
          </div>
          <div className="flex items-center gap-1">
            <Button
              size="sm"
              variant="outline"
              className="size-8 has-[>svg]:px-0"
              aria-label={`یکی کمتر ${line.name}`}
              disabled={line.quantity <= 1}
              onClick={() => onQuantityChange(line.productId, line.quantity - 1)}
            >
              <Minus aria-hidden />
            </Button>
            <QuantityInput
              line={line}
              onCommit={(quantity) => onQuantityChange(line.productId, quantity)}
            />
            <Button
              size="sm"
              variant="outline"
              className="size-8 has-[>svg]:px-0"
              aria-label={`یکی بیشتر ${line.name}`}
              disabled={line.quantity >= cafeLimits.maxQuantity}
              onClick={() => onQuantityChange(line.productId, line.quantity + 1)}
            >
              <Plus aria-hidden />
            </Button>
            <span className="ms-2 text-xs text-muted-foreground">
              × {formatMoney(line.unitPrice)}
            </span>
            <Button
              size="sm"
              variant="ghost"
              className="ms-auto size-8 has-[>svg]:px-0"
              aria-label={`حذف ${line.name}`}
              onClick={() => onRemove(line.productId)}
            >
              <Trash2 aria-hidden />
            </Button>
          </div>
        </li>
      ))}
    </ul>
  );
}

/**
 * A quantity typed with Persian or English digits. A valid number is taken as it is typed; a box
 * left empty or holding nonsense goes back to the last good number when it loses focus.
 */
function QuantityInput({
  line,
  onCommit,
}: {
  line: CartLine;
  onCommit: (quantity: number) => void;
}) {
  const [text, setText] = useState(toPersianDigits(line.quantity));

  // The quantity can change from outside too (the buttons, a second tap on the product). The box
  // follows it then, but not while it already says that number — so typing "۱۲" is not rewritten
  // under the caret after the "۱".
  const [shown, setShown] = useState(line.quantity);
  if (line.quantity !== shown) {
    setShown(line.quantity);
    if (parseQuantity(text) !== line.quantity) {
      setText(toPersianDigits(line.quantity));
    }
  }

  return (
    <Input
      aria-label={`تعداد ${line.name}`}
      inputMode="numeric"
      className="h-8 w-14 text-center"
      value={text}
      onChange={(event) => {
        setText(event.target.value);
        const quantity = parseQuantity(event.target.value);
        if (quantity !== null && quantity !== line.quantity) {
          onCommit(quantity);
        }
      }}
      onBlur={() => setText(toPersianDigits(line.quantity))}
    />
  );
}
