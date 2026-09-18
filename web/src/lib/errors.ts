/**
 * API error code → Persian message.
 *
 * The API never sends Persian text. It sends a stable `code` (`Auth.InvalidCredentials`) and
 * an English `detail` for developers; this file is the only place that decides what a user
 * reads. A test on the .NET side (ErrorCatalogTests) fails when the API gains a code that is
 * missing here, so a new error cannot reach the screen as English or as a bare code.
 */

/** The ProblemDetails fields the frontend reads. See docs/ARCHITECTURE.md, "Errors". */
export interface ApiProblem {
  status?: number | string;
  code?: string;
  detail?: string;
  correlationId?: string;
  errors?: Record<string, { code: string; description: string }[]>;
}

export const errorMessages: Record<string, string> = {
  // General
  "General.ValidationFailed": "لطفاً خطاهای فرم را برطرف کنید.",
  "General.Unexpected": "خطای غیرمنتظره‌ای رخ داد.",
  "General.TooManyRequests": "تعداد تلاش‌ها زیاد است. کمی بعد دوباره امتحان کنید.",

  // Login and session
  "Auth.InvalidCredentials": "نام کاربری یا رمز عبور اشتباه است.",
  "Auth.LockedOut":
    "حساب به دلیل تلاش‌های ناموفق زیاد موقتاً قفل شده است. ۱۵ دقیقه دیگر دوباره امتحان کنید.",
  "Auth.UserInactive": "این حساب غیرفعال شده است. با مدیر باشگاه تماس بگیرید.",
  "Auth.RefreshTokenInvalid": "نشست شما به پایان رسیده است. دوباره وارد شوید.",
  "Auth.Unauthenticated": "برای ادامه وارد شوید.",
  "Auth.PasswordChangeRequired": "ابتدا باید رمز عبور خود را تغییر دهید.",
  "Auth.Forbidden": "اجازهٔ انجام این کار را ندارید.",

  // Passwords
  "Auth.UserNameRequired": "نام کاربری را وارد کنید.",
  "Auth.UserNameTooLong": "نام کاربری بیش از حد طولانی است.",
  "Auth.PasswordRequired": "رمز عبور را وارد کنید.",
  "Auth.CurrentPasswordRequired": "رمز عبور فعلی را وارد کنید.",
  "Auth.NewPasswordRequired": "رمز عبور جدید را وارد کنید.",
  "Auth.PasswordTooShort": "رمز عبور باید دست‌کم ۸ نویسه باشد.",
  "Auth.PasswordTooLong": "رمز عبور بیش از حد طولانی است.",
  "Auth.PasswordRequiresLetterAndDigit": "رمز عبور باید دست‌کم یک حرف و یک رقم داشته باشد.",
  "Auth.CurrentPasswordIncorrect": "رمز عبور فعلی اشتباه است.",
  "Auth.PasswordUnchanged": "رمز عبور جدید باید با رمز فعلی فرق داشته باشد.",
  "Auth.PasswordRejected": "این رمز عبور پذیرفته نشد.",

  // Staff accounts
  "Staff.NotFound": "حساب کارمند پیدا نشد.",
  "Staff.UserNameTaken": "این نام کاربری قبلاً استفاده شده است.",
  "Staff.Rejected": "اطلاعات حساب پذیرفته نشد.",
  "Staff.UserNameRequired": "نام کاربری را وارد کنید.",
  "Staff.UserNameLength": "نام کاربری باید بین ۳ تا ۵۰ نویسه باشد.",
  "Staff.UserNameInvalidCharacters":
    "نام کاربری فقط می‌تواند حروف انگلیسی، رقم و نویسه‌های - . _ @ + داشته باشد.",
  "Staff.FullNameRequired": "نام و نام خانوادگی را وارد کنید.",
  "Staff.FullNameTooLong": "نام بیش از حد طولانی است.",
  "Staff.TemporaryPasswordRequired": "رمز عبور موقت را وارد کنید.",

  // Refresh token rules inside the domain. The API reports them as Auth.RefreshTokenInvalid,
  // but they are real codes, so they get a message rather than an exception in the catalogue test.
  "RefreshTokens.Revoked": "نشست شما به پایان رسیده است. دوباره وارد شوید.",
  "RefreshTokens.Expired": "نشست شما به پایان رسیده است. دوباره وارد شوید.",

  // Paging
  "Paging.PageInvalid": "شمارهٔ صفحه نامعتبر است.",
  "Paging.PageSizeInvalid": "تعداد ردیف‌های هر صفحه نامعتبر است.",
};

/** Shown when the server could not be reached at all. */
export const networkErrorMessage = "ارتباط با سرور برقرار نشد. اتصال را بررسی کنید.";

function isProblem(value: unknown): value is ApiProblem {
  return typeof value === "object" && value !== null;
}

/**
 * The Persian sentence for a failed request. An unknown code still gets a Persian sentence,
 * plus the correlation id, which is what the Owner can quote when reporting it.
 */
export function errorMessage(problem: unknown): string {
  if (problem instanceof TypeError) {
    return networkErrorMessage;
  }

  if (isProblem(problem) && problem.code !== undefined) {
    const message = errorMessages[problem.code];
    if (message !== undefined) {
      return message;
    }
  }

  const correlationId = isProblem(problem) ? problem.correlationId : undefined;

  return correlationId === undefined
    ? errorMessages["General.Unexpected"]!
    : `${errorMessages["General.Unexpected"]} کد پیگیری: ${correlationId}`;
}

/**
 * Per-field Persian messages from a 400's `errors`, keyed by the JSON property name, which is
 * the same name the form field uses. The first error of each field is enough to show.
 */
export function fieldErrors(problem: unknown): Record<string, string> {
  if (!isProblem(problem) || problem.errors === undefined) {
    return {};
  }

  const result: Record<string, string> = {};
  for (const [field, errors] of Object.entries(problem.errors)) {
    const first = errors[0];
    if (first !== undefined) {
      result[field] = errorMessages[first.code] ?? first.description;
    }
  }

  return result;
}
