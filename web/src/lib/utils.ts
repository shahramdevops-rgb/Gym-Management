import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

/**
 * Merges class names and lets a later Tailwind utility win over an earlier one in the same
 * group — the convention every shadcn/ui component is written against.
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}
