import type { FieldErrors, FieldValues, Path, Resolver, UseFormSetError } from "react-hook-form";
import type { z } from "zod";

import { errorMessage, fieldErrors } from "./errors";

/**
 * Lets React Hook Form validate with a Zod schema.
 *
 * The usual package for this (@hookform/resolvers) is not on the approved list in
 * docs/ARCHITECTURE.md, and the part this app needs is these few lines. Forms here are flat,
 * so an issue's path joined with dots is exactly the field name.
 */
export function zodResolver<TValues extends FieldValues>(
  schema: z.ZodType<TValues>,
): Resolver<TValues> {
  return async (values) => {
    const result = await schema.safeParseAsync(values);
    if (result.success) {
      return { values: result.data, errors: {} };
    }

    const errors: Record<string, { type: string; message: string }> = {};
    for (const issue of result.error.issues) {
      const path = issue.path.join(".");
      errors[path] ??= { type: issue.code, message: issue.message };
    }

    return { values: {}, errors: errors as FieldErrors<TValues> };
  };
}

/**
 * Puts a failed request's errors on the form: each field error under its field, and anything
 * that belongs to no field under `root`, which the form shows above its button.
 *
 * `codeFields` moves a whole-request error to the field it is about, for example
 * `Auth.CurrentPasswordIncorrect` → the current password box.
 */
export function applyServerErrors<TValues extends FieldValues>(
  problem: unknown,
  setError: UseFormSetError<TValues>,
  codeFields: Partial<Record<string, Path<TValues>>> = {},
) {
  const perField = fieldErrors(problem);
  for (const [field, message] of Object.entries(perField)) {
    setError(field as Path<TValues>, { type: "server", message });
  }

  if (Object.keys(perField).length > 0) {
    return;
  }

  const code =
    typeof problem === "object" && problem !== null && "code" in problem ? problem.code : undefined;
  const field = typeof code === "string" ? codeFields[code] : undefined;

  if (field !== undefined) {
    setError(field, { type: "server", message: errorMessage(problem) });
  } else {
    setError("root.server" as Path<TValues>, { type: "server", message: errorMessage(problem) });
  }
}
