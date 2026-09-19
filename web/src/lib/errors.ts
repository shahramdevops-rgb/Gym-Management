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
  "Staff.ChangedConcurrently": "این حساب هم‌زمان تغییر کرد. دوباره امتحان کنید.",
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

  // Members
  "Members.NotFound": "عضو پیدا نشد.",
  "Members.PhoneAlreadyExists": "این شماره موبایل قبلاً برای عضو دیگری ثبت شده است.",
  "Members.PhoneInvalid": "شماره موبایل معتبر نیست.",
  "Members.PhoneNotMobile": "شماره باید موبایل باشد؛ شمارهٔ ثابت پذیرفته نمی‌شود.",
  "Members.PhoneNotIranian": "فقط شماره موبایل ایران پذیرفته می‌شود.",
  "Members.PhoneRequired": "شماره موبایل را وارد کنید.",
  "Members.FullNameRequired": "نام و نام خانوادگی را وارد کنید.",
  "Members.FullNameTooLong": "نام بیش از حد طولانی است.",
  "Members.NotesTooLong": "یادداشت بیش از حد طولانی است.",
  "Members.SearchTooShort": "برای جستجو دست‌کم ۲ حرف وارد کنید.",
  "Members.SearchTooLong": "متن جستجو بیش از حد طولانی است.",
  "Members.Inactive": "این عضو غیرفعال است.",
  "Members.ChangedConcurrently":
    "این عضو هم‌زمان توسط شخص دیگری ویرایش شد. صفحه را دوباره باز کنید و دوباره امتحان کنید.",

  // Plans
  "Plans.NotFound": "پلن پیدا نشد.",
  "Plans.NameAlreadyExists": "پلن دیگری با همین نام وجود دارد.",
  "Plans.NameRequired": "نام پلن را وارد کنید.",
  "Plans.NameTooLong": "نام پلن بیش از حد طولانی است.",
  "Plans.DurationInvalid": "مدت پلن باید بین ۱ تا ۳۶۵ روز باشد.",
  "Plans.SessionCountInvalid": "تعداد جلسات باید بین ۱ تا ۳۶۵ باشد، یا برای نامحدود خالی بماند.",
  "Plans.PriceNegative": "قیمت نمی‌تواند منفی باشد.",
  "Plans.PriceTooLarge": "قیمت بیش از حد بزرگ است.",
  "Plans.PriceTooManyDecimals": "قیمت حداکثر می‌تواند ۲ رقم اعشار داشته باشد.",
  "Plans.Inactive": "این پلن غیرفعال است و قابل فروش نیست.",
  "Plans.ChangedConcurrently":
    "این پلن هم‌زمان توسط شخص دیگری ویرایش شد. صفحه را دوباره باز کنید و دوباره امتحان کنید.",

  // Subscriptions
  "Subscriptions.NotFound": "اشتراک پیدا نشد.",
  "Subscriptions.PlanRequired": "یک پلن انتخاب کنید.",
  "Subscriptions.NothingToRenew": "این عضو اشتراکی برای تمدید ندارد.",
  "Subscriptions.ChangedConcurrently":
    "اشتراک‌های این عضو هم‌زمان توسط شخص دیگری تغییر کرد. دوباره امتحان کنید.",
  "Subscriptions.NotStarted": "اشتراک هنوز شروع نشده است.",
  "Subscriptions.Expired": "اشتراک به پایان رسیده است.",
  "Subscriptions.NoSessionsLeft": "جلسات این اشتراک تمام شده است.",
  "Subscriptions.Frozen": "اشتراک فریز شده است.",
  "Subscriptions.Cancelled": "اشتراک لغو شده است.",
  "Subscriptions.NotFrozen": "اشتراک فریز نشده است.",
  "Subscriptions.FreezeLimitReached": "همهٔ روزهای مجاز فریز این اشتراک استفاده شده است.",
  "Subscriptions.CancelReasonRequired": "دلیل لغو را وارد کنید.",
  "Subscriptions.CancelReasonTooLong": "دلیل لغو بیش از حد طولانی است.",

  // Payments
  "Payments.AmountNotPositive": "مبلغ باید بزرگ‌تر از صفر باشد.",
  "Payments.AmountTooLarge": "مبلغ بیش از حد بزرگ است.",
  "Payments.AmountTooManyDecimals": "مبلغ حداکثر می‌تواند ۲ رقم اعشار داشته باشد.",
  "Payments.ReferenceNumberTooLong": "شماره پیگیری بیش از حد طولانی است.",
  "Payments.MethodInvalid": "روش پرداخت معتبر نیست.",
  "Payments.Overpayment": "این پرداخت از مبلغ اشتراک بیشتر می‌شود.",

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
