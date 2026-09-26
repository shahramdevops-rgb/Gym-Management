import type { ComponentProps } from "react";

import { SelectField } from "@/components/FormField";

import { paymentMethodLabels, paymentMethods } from "../api";

type PaymentMethodFieldProps = Omit<ComponentProps<typeof SelectField>, "label" | "children"> & {
  label?: string;
};

/**
 * The "روش پرداخت" list every money form uses. It starts on an empty option, never on a method,
 * so the desk has to look at it and pick one (BUSINESS_RULES.md §5); the schema refuses the empty
 * one. The empty option is not `disabled`: a browser skips a disabled first option and shows the
 * next one, which would put a method back in front of the desk.
 */
export function PaymentMethodField({
  label = "روش پرداخت",
  ...selectProps
}: PaymentMethodFieldProps) {
  return (
    <SelectField label={label} {...selectProps}>
      <option value="">انتخاب کنید…</option>
      {paymentMethods.map((method) => (
        <option key={method} value={method}>
          {paymentMethodLabels[method]}
        </option>
      ))}
    </SelectField>
  );
}
