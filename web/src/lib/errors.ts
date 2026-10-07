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
  "Auth.PasswordTooShort": "رمز عبور باید دست‌کم ۱۲ نویسه باشد.",
  "Auth.PasswordTooLong": "رمز عبور بیش از حد طولانی است.",
  "Auth.PasswordNotEnglish":
    "رمز عبور فقط می‌تواند حروف، رقم و علامت‌های انگلیسی داشته باشد. کیبورد را انگلیسی کنید.",
  "Auth.PasswordContainsUserName": "نام کاربری نباید داخل رمز عبور باشد.",
  "Auth.PasswordContainsGymName":
    "نام باشگاه نباید داخل رمز عبور باشد، حتی با غلط املایی؛ به‌راحتی حدس زده می‌شود.",
  "Auth.PasswordTooSimple": "رمز عبور تکراری یا پشت سر هم است. رمز دیگری انتخاب کنید.",
  "Auth.PasswordTooCommon":
    "این رمز عبور جزو رمزهای رایج و لو رفته است و به‌راحتی حدس زده می‌شود. رمز دیگری انتخاب کنید.",
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
  "Staff.UserNameGuessable":
    "این نام کاربری به‌راحتی حدس زده می‌شود (مثل مدیر، مالک یا تست). نامی مثل نام خود شخص انتخاب کنید.",
  "Staff.FullNameRequired": "نام و نام خانوادگی را وارد کنید.",
  "Staff.FullNameTooLong": "نام بیش از حد طولانی است.",
  "Staff.TemporaryPasswordRequired": "رمز عبور موقت را وارد کنید.",

  // The server console's account commands (./server.sh unlock | set-password | rename). They
  // print in English on the server and never reach the web app today, but they are real codes,
  // so they get a message rather than an exception in the catalogue test.
  "Accounts.UserNotFound": "حسابی با این نام کاربری پیدا نشد.",
  "Accounts.UserNameTaken": "این نام کاربری قبلاً استفاده شده است.",
  "Accounts.UserNameInvalid":
    "نام کاربری باید بین ۳ تا ۵۰ نویسه و فقط از حروف انگلیسی، رقم و نویسه‌های - . _ @ + باشد.",
  "Accounts.UserNameGuessable": "این نام کاربری به‌راحتی حدس زده می‌شود. نام دیگری انتخاب کنید.",
  "Accounts.ChangedConcurrently": "این حساب هم‌زمان تغییر کرد. دوباره امتحان کنید.",
  "Accounts.Rejected": "اطلاعات حساب پذیرفته نشد.",

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
  "Members.BirthDateRequired": "تاریخ تولد را وارد کنید.",
  "Members.BirthDateInFuture": "تاریخ تولد نمی‌تواند در آینده باشد.",
  "Members.BirthDateTooOld": "تاریخ تولد نمی‌تواند بیش از ۱۲۰ سال پیش باشد.",
  "Members.SearchTooShort": "برای جستجو دست‌کم ۲ حرف وارد کنید.",
  "Members.SearchTooLong": "متن جستجو بیش از حد طولانی است.",
  "Members.Inactive": "این عضو غیرفعال است.",
  "Members.ChangedConcurrently":
    "این عضو هم‌زمان توسط شخص دیگری ویرایش شد. صفحه را دوباره باز کنید و دوباره امتحان کنید.",

  // Pricing (BUSINESS_RULES.md §3)
  "Pricing.PriceNegative": "قیمت نمی‌تواند منفی باشد.",
  "Pricing.PriceTooLarge": "قیمت بیش از حد بزرگ است.",
  "Pricing.PriceTooManyDecimals": "قیمت حداکثر می‌تواند ۲ رقم اعشار داشته باشد.",
  "Pricing.SessionPriceNotSet":
    "قیمت هر جلسه هنوز تعیین نشده است. مدیر باید آن را در صفحهٔ تنظیمات وارد کند.",
  "Pricing.SingleVisitPriceNotSet":
    "قیمت تک‌جلسهٔ آزاد هنوز تعیین نشده است. مدیر باید آن را در صفحهٔ تنظیمات وارد کند.",
  "Pricing.ChangedConcurrently":
    "قیمت‌ها هم‌زمان توسط شخص دیگری تغییر کرد. صفحه را دوباره باز کنید و دوباره امتحان کنید.",

  // SMS settings (BUSINESS_RULES.md §10 *SMS settings*)
  "Sms.SubscriptionExpiringDaysOutOfRange": "تعداد روز باید از ۱ تا ۳۰ باشد.",
  "Sms.LowSessionsOutOfRange": "تعداد جلسه باید از ۱ تا ۱۰ باشد.",
  "Sms.BirthdayDaysOutOfRange": "تعداد روز باید از ۰ تا ۷ باشد (۰ یعنی خود روز تولد).",
  "Sms.PayableDueDaysOutOfRange": "تعداد روز باید از ۰ تا ۳۰ باشد (۰ یعنی خود روز سررسید).",
  "Sms.SendTimeOutOfRange": "ساعت ارسال باید بین ۸:۰۰ و ۲۲:۰۰ و روی ربع ساعت باشد.",
  "Sms.TemplateNameInvalid":
    "نام قالب فقط حروف انگلیسی و عدد دارد؛ فاصله، «_» و حروف فارسی پذیرفته نمی‌شود.",
  "Sms.TemplateNameTooLong": "نام قالب حداکثر ۱۰۰ نویسه است.",
  "Sms.SettingsIncomplete": "برای روشن کردن این پیامک، همهٔ خانه‌هایش را پر کنید.",
  "Sms.ChangedConcurrently":
    "تنظیمات پیامک هم‌زمان توسط شخص دیگری تغییر کرد. صفحه را دوباره باز کنید و دوباره امتحان کنید.",

  // Subscriptions
  "Subscriptions.NotFound": "اشتراک پیدا نشد.",
  "Subscriptions.SessionCountTooLow": "تعداد جلسات باید حداقل ۵ باشد.",
  "Subscriptions.SessionCountTooHigh": "تعداد جلسات حداکثر ۱۴۰ است.",
  "Subscriptions.PriceTooLarge": "قیمت این پلن بیش از حد بزرگ است.",
  "Subscriptions.NothingToRenew": "این عضو اشتراکی برای تمدید ندارد.",
  "Subscriptions.ChangedConcurrently":
    "اشتراک‌های این عضو هم‌زمان توسط شخص دیگری تغییر کرد. دوباره امتحان کنید.",
  "Subscriptions.NotStarted": "اشتراک هنوز شروع نشده است.",
  "Subscriptions.NextStartsTomorrow":
    "جلسات اشتراک امروز تمام شد. اشتراک بعدی از فردا قابل استفاده است.",
  "Subscriptions.Expired": "اشتراک به پایان رسیده است.",
  "Subscriptions.NoSessionsLeft": "جلسات این اشتراک تمام شده است.",
  "Subscriptions.Frozen": "اشتراک فریز شده است.",
  "Subscriptions.Cancelled": "اشتراک لغو شده است.",
  "Subscriptions.NotFrozen": "اشتراک فریز نشده است.",
  "Subscriptions.SingleSessionNotFreezable": "اشتراک تک‌جلسه‌ای قابل فریز شدن نیست.",
  "Subscriptions.FreezeLimitReached": "همهٔ روزهای مجاز فریز این اشتراک استفاده شده است.",
  "Subscriptions.CancelReasonRequired": "دلیل لغو را وارد کنید.",
  "Subscriptions.CancelReasonTooLong": "دلیل لغو بیش از حد طولانی است.",
  "Subscriptions.AlreadyUsed": "از این اشتراک استفاده شده است و دیگر نمی‌توان آن را لغو کرد.",

  // Payments
  "Payments.AmountNotPositive": "مبلغ باید بزرگ‌تر از صفر باشد.",
  "Payments.AmountTooLarge": "مبلغ بیش از حد بزرگ است.",
  "Payments.AmountTooManyDecimals": "مبلغ حداکثر می‌تواند ۲ رقم اعشار داشته باشد.",
  "Payments.ReferenceNumberTooLong": "شماره پیگیری بیش از حد طولانی است.",
  "Payments.MethodInvalid": "روش پرداخت معتبر نیست.",
  "Payments.Overpayment": "این پرداخت از باقی‌ماندهٔ این مورد بیشتر می‌شود.",
  "Payments.RefundReasonRequired": "دلیل استرداد را وارد کنید.",
  "Payments.RefundReasonTooLong": "دلیل استرداد بیش از حد طولانی است.",
  "Payments.RefundExceedsNetPaid": "این استرداد از مبلغ پرداخت‌شدهٔ این مورد بیشتر است.",
  "Payments.RefundAfterUse": "از این اشتراک استفاده شده است و مبلغ آن قابل استرداد نیست.",
  "Payments.HistoryTooFarBack": "کارمندان فقط پرداخت‌های امروز و ۳ روز قبل از آن را می‌بینند.",
  "Payments.InvalidDateRange": "بازهٔ تاریخ نامعتبر است.",
  "Payments.InvalidSource": "بابت پرداخت معتبر نیست.",
  "Payments.InvalidPaidFilter": "فیلتر پرداخت معتبر نیست.",

  // Settling several items at once (تسویه یکجا)
  "Settlements.NoItems": "دست‌کم یک مورد را برای تسویه انتخاب کنید.",
  "Settlements.DuplicateItem": "یک مورد دو بار در تسویه آمده است.",
  "Settlements.DebtChanged":
    "بدهی این عضو در این فاصله تغییر کرد و چیزی ثبت نشد. فهرست تازه را بررسی کنید و دوباره تأیید کنید.",

  // Lockers
  "Lockers.NotFound": "کمد پیدا نشد.",
  "Lockers.Occupied": "این کمد اشغال است و نمی‌توان آن را از سرویس خارج کرد.",
  "Lockers.OutOfService": "این کمد خارج از سرویس است. کمد دیگری انتخاب کنید.",
  "Lockers.ChangedConcurrently":
    "این کمد هم‌زمان توسط شخص دیگری تغییر کرد. صفحه را دوباره باز کنید و دوباره امتحان کنید.",

  // Attendance
  "Attendance.NoSubscription": "این عضو اشتراکی ندارد.",
  "Attendance.AlreadyCheckedIn": "این عضو هم‌اکنون داخل باشگاه است.",
  "Attendance.ChangedConcurrently": "ورود هم‌زمان با شخص دیگری ثبت شد. دوباره امتحان کنید.",
  "Attendance.LockerTaken": "این کمد را کس دیگری گرفته است. کمد دیگری انتخاب کنید.",
  "Attendance.LockersStillFree":
    "هنوز کمد آزاد هست؛ ورود بدون کمد فقط وقتی است که همهٔ کمدها پر باشند.",
  "Attendance.ReserveFull": "هر ۱۵ جای ورود بدون کمد پر است.",
  "Attendance.SameLocker": "این مراجعه همین حالا همین کمد را دارد.",
  "Attendance.NotFound": "ورود و خروج یافت نشد.",
  "Attendance.NotOpen": "این ورود قبلاً بسته شده است.",
  "Attendance.CancelWindowExpired": "مهلت لغو این ورود گذشته است.",
  "Attendance.InvalidDateRange": "بازهٔ تاریخ نامعتبر است.",
  "Attendance.SaleInvalid": "فروش همراه ورود نامعتبر است.",
  "Attendance.CancelChoiceRequired": "مشخص نشده است با خریدهای این مراجعه چه شود.",
  "Attendance.CafeOrderNotOnVisit":
    "یکی از خریدهای بوفه در این فاصله تغییر کرده است. پنجره را ببندید و دوباره امتحان کنید.",
  "Attendance.SaleNotOnVisit":
    "یکی از فروش‌های فروشگاه، آنالیز یا متفرقه در این فاصله تغییر کرده است. پنجره را ببندید و دوباره امتحان کنید.",
  "Attendance.GuestNameRequired": "نام و نام خانوادگی مهمان را وارد کنید.",
  "Attendance.GuestNameTooLong": "نام مهمان حداکثر ۲۰۰ نویسه است.",
  "Attendance.GuestHasUnpaidPurchases":
    "مهمان خرید پرداخت‌نشده دارد. ابتدا با «تسویه یکجا» آن را پرداخت کنید.",
  "Attendance.NotGuestVisit": "این ورود متعلق به مهمان نیست.",
  "Attendance.CardioChargeMissing":
    "این ورود فقط هوازی است: پیش از ثبت خروج، مبلغ هوازی را ثبت کنید (پرداخت‌نشده هم بدهی عضو می‌شود).",

  // Gym services (هوازی)
  "ServiceCharges.NotFound": "هزینهٔ خدمات پیدا نشد.",
  "ServiceCharges.KindInvalid": "نوع خدمت معتبر نیست.",
  "ServiceCharges.AmountNotPositive": "مبلغ باید بزرگ‌تر از صفر باشد.",
  "ServiceCharges.AmountTooLarge": "مبلغ بیش از حد بزرگ است.",
  "ServiceCharges.AmountTooManyDecimals": "مبلغ حداکثر می‌تواند ۲ رقم اعشار داشته باشد.",
  "ServiceCharges.VisitNotOpen":
    "این ورود بسته شده است؛ برای اصلاح مبلغ باید آن را با ذکر دلیل ابطال کنید.",
  "ServiceCharges.AlreadyCharged": "برای این ورود قبلاً مبلغ هوازی ثبت شده است.",
  "ServiceCharges.AlreadyVoided": "این مبلغ قبلاً ابطال شده است.",
  "ServiceCharges.AlreadyPaid":
    "برای این مبلغ پرداختی ثبت شده است؛ برای اصلاح آن را با ذکر دلیل ابطال کنید.",
  "ServiceCharges.VoidReasonRequired": "دلیل ابطال را وارد کنید.",
  "ServiceCharges.VoidReasonTooLong": "دلیل ابطال بیش از حد طولانی است.",
  "ServiceCharges.ChangedConcurrently":
    "این مبلغ هم‌زمان توسط شخص دیگری تغییر کرد. صفحه را دوباره باز کنید و دوباره امتحان کنید.",
  "ServiceCharges.InvalidDateRange": "بازهٔ تاریخ نامعتبر است.",
  "ServiceCharges.DescriptionRequired": "نام کالا را وارد کنید.",
  "ServiceCharges.DescriptionTooLong": "نام کالا حداکثر ۱۰۰ نویسه است.",
  "ServiceCharges.QuantityInvalid": "تعداد باید عددی از ۱ تا ۹۹۹ باشد.",
  "ServiceCharges.ShopItemsRequired": "دست‌کم یک کالا وارد کنید.",
  "ServiceCharges.TooManyShopItems": "در هر فروش حداکثر ۵۰ کالا ثبت می‌شود.",
  "ServiceCharges.SaleNotEditable":
    "فروش فروشگاه، آنالیز یا متفرقه ویرایش نمی‌شود؛ آن را با ذکر دلیل ابطال کنید و دوباره ثبت کنید.",

  // Cafe: categories and products (no stock anywhere — BUSINESS_RULES.md §8)
  "ProductCategories.NotFound": "دسته‌بندی پیدا نشد.",
  "ProductCategories.NameAlreadyExists": "دسته‌بندی دیگری با همین نام وجود دارد.",
  "ProductCategories.NameRequired": "نام دسته‌بندی را وارد کنید.",
  "ProductCategories.NameTooLong": "نام دسته‌بندی بیش از حد طولانی است.",
  "ProductCategories.NotEmpty":
    "این دسته‌بندی محصول دارد و حذف نمی‌شود. اول محصول‌ها را به دسته‌بندی دیگری منتقل کنید.",
  "ProductCategories.ChangedConcurrently":
    "این دسته‌بندی هم‌زمان توسط شخص دیگری تغییر کرد. صفحه را دوباره باز کنید و دوباره امتحان کنید.",
  "Products.NotFound": "محصول پیدا نشد.",
  "Products.NameAlreadyExists": "محصول دیگری با همین نام وجود دارد.",
  "Products.NameRequired": "نام محصول را وارد کنید.",
  "Products.NameTooLong": "نام محصول بیش از حد طولانی است.",
  "Products.CategoryRequired": "دسته‌بندی محصول را انتخاب کنید.",
  "Products.CategoryNotFound": "دسته‌بندی انتخاب‌شده پیدا نشد.",
  "Products.PriceNegative": "قیمت نمی‌تواند منفی باشد.",
  "Products.PriceTooLarge": "قیمت بیش از حد بزرگ است.",
  "Products.PriceTooManyDecimals": "قیمت حداکثر می‌تواند ۲ رقم اعشار داشته باشد.",
  "Products.Inactive": "این محصول غیرفعال است و قابل فروش نیست.",
  "Products.ChangedConcurrently":
    "این محصول هم‌زمان توسط شخص دیگری تغییر کرد. صفحه را دوباره باز کنید و دوباره امتحان کنید.",

  // Cafe orders (BUSINESS_RULES.md §8)
  "CafeOrders.NotFound": "سفارش پیدا نشد.",
  "CafeOrders.NoItems": "سبد خرید خالی است.",
  "CafeOrders.TooManyItems": "تعداد ردیف‌های سفارش بیش از حد مجاز است.",
  "CafeOrders.QuantityInvalid": "تعداد هر محصول باید بین ۱ تا ۹۹۹ باشد.",
  "CafeOrders.DuplicateProduct": "یک محصول دو بار در سبد آمده است؛ آن را در یک ردیف جمع کنید.",
  "CafeOrders.ProductNotFound": "یکی از محصول‌های سبد دیگر وجود ندارد.",
  "CafeOrders.WalkInMustBePaidInFull":
    "سفارش بدون عضو باید همان لحظه کامل پرداخت شود. برای پرداخت بعدی، عضو را انتخاب کنید.",
  "CafeOrders.PaidMoreThanTheOrder": "مبلغ پرداختی از مبلغ سفارش بیشتر است.",
  "CafeOrders.AlreadyCancelled": "این سفارش قبلاً لغو شده است.",
  "CafeOrders.CancelReasonRequired": "دلیل لغو را وارد کنید.",
  "CafeOrders.CancelReasonTooLong": "دلیل لغو بیش از حد طولانی است.",
  "CafeOrders.InvalidDateRange": "بازهٔ تاریخ نامعتبر است.",
  "CafeOrders.VisitNotOpen": "این عضو دیگر داخل باشگاه نیست؛ خرید را از صفحهٔ بوفه ثبت کنید.",
  "CafeOrders.VisitOfAnotherMember": "این مراجعه متعلق به عضو دیگری است.",
  "CafeOrders.ChangedConcurrently":
    "این سفارش هم‌زمان توسط شخص دیگری تغییر کرد. صفحه را دوباره باز کنید و دوباره امتحان کنید.",

  // Expenses (BUSINESS_RULES.md §9)
  "ExpenseCategories.NotFound": "دسته‌بندی هزینه پیدا نشد.",
  "ExpenseCategories.NameAlreadyExists": "دسته‌بندی هزینهٔ دیگری با همین نام وجود دارد.",
  "ExpenseCategories.NameRequired": "نام دسته‌بندی را وارد کنید.",
  "ExpenseCategories.NameTooLong": "نام دسته‌بندی بیش از حد طولانی است.",
  "ExpenseCategories.ChangedConcurrently":
    "این دسته‌بندی هم‌زمان توسط شخص دیگری تغییر کرد. صفحه را دوباره باز کنید و دوباره امتحان کنید.",
  "Expenses.NotFound": "هزینه پیدا نشد.",
  "Expenses.AmountNotPositive": "مبلغ باید بزرگ‌تر از صفر باشد.",
  "Expenses.AmountTooLarge": "مبلغ بیش از حد بزرگ است.",
  "Expenses.AmountTooManyDecimals": "مبلغ حداکثر می‌تواند ۲ رقم اعشار داشته باشد.",
  "Expenses.CategoryRequired": "دسته‌بندی هزینه را انتخاب کنید.",
  "Expenses.CategoryNotFound": "دسته‌بندی انتخاب‌شده پیدا نشد.",
  "Expenses.DateInFuture": "تاریخ هزینه نمی‌تواند بعد از امروز باشد.",
  "Expenses.DescriptionRequired": "شرح هزینه را وارد کنید.",
  "Expenses.DescriptionTooLong": "شرح هزینه بیش از حد طولانی است.",
  "Expenses.ReferenceNumberTooLong": "شمارهٔ مرجع بیش از حد طولانی است.",
  "Expenses.AlreadyVoided": "این هزینه باطل شده است و دیگر تغییر نمی‌کند.",
  "Expenses.VoidReasonRequired": "دلیل ابطال را وارد کنید.",
  "Expenses.VoidReasonTooLong": "دلیل ابطال بیش از حد طولانی است.",
  "Expenses.InvalidDateRange": "بازهٔ تاریخ نامعتبر است.",
  "Expenses.ChangedConcurrently":
    "این هزینه هم‌زمان توسط شخص دیگری تغییر کرد. صفحه را دوباره باز کنید و دوباره امتحان کنید.",
  "Expenses.LinkedToPayable":
    "این هزینه از پرداخت یک چک یا قسط ثبت شده است. برای تغییرش، آن چک یا قسط را در «چک و قسط» به در انتظار برگردانید.",

  // Cheques and instalments (BUSINESS_RULES.md §9 *Cheques and instalments*)
  "Payables.NotFound": "چک یا قسط پیدا نشد.",
  "Payables.AmountNotPositive": "مبلغ باید بزرگ‌تر از صفر باشد.",
  "Payables.AmountTooLarge": "مبلغ بیش از حد بزرگ است.",
  "Payables.AmountTooManyDecimals": "مبلغ حداکثر می‌تواند ۲ رقم اعشار داشته باشد.",
  "Payables.PayeeRequired": "نام گیرنده را وارد کنید.",
  "Payables.PayeeTooLong": "نام گیرنده بیش از حد طولانی است.",
  "Payables.DescriptionRequired": "شرح را وارد کنید.",
  "Payables.DescriptionTooLong": "شرح بیش از حد طولانی است.",
  "Payables.CategoryRequired": "دسته‌بندی هزینه را انتخاب کنید.",
  "Payables.CategoryNotFound": "دسته‌بندی انتخاب‌شده پیدا نشد.",
  "Payables.InstallmentNumbersRequired": "شمارهٔ قسط و تعداد کل قسط‌ها را وارد کنید.",
  "Payables.InstallmentNumbersOnlyForInstallments": "چک شمارهٔ قسط ندارد.",
  "Payables.InstallmentCountOutOfRange": "تعداد کل قسط‌ها باید بین ۱ و ۳۶۰ باشد.",
  "Payables.InstallmentNumberOutOfRange": "شمارهٔ قسط باید بین ۱ و تعداد کل قسط‌ها باشد.",
  "Payables.ChequeNotDueYet": "چک پیش از تاریخش پاس نمی‌شود.",
  "Payables.AlreadyPaid": "این مورد پرداخت شده است. برای تغییرش، اول آن را به در انتظار برگردانید.",
  "Payables.AlreadyCancelled": "این مورد باطل شده است و دیگر تغییر نمی‌کند.",
  "Payables.NotPaid": "فقط چک یا قسطِ پرداخت‌شده به در انتظار برمی‌گردد.",
  "Payables.CancelReasonRequired": "دلیل ابطال را وارد کنید.",
  "Payables.CancelReasonTooLong": "دلیل ابطال بیش از حد طولانی است.",
  "Payables.RevertReasonRequired": "دلیل برگشت را وارد کنید.",
  "Payables.RevertReasonTooLong": "دلیل برگشت بیش از حد طولانی است.",
  "Payables.ChangedConcurrently":
    "این مورد هم‌زمان توسط شخص دیگری تغییر کرد. صفحه را دوباره باز کنید و دوباره امتحان کنید.",

  // SMS
  "Notifications.NotPending": "این پیامک دیگر در صف ارسال نیست.",
  "Notifications.NotSent": "وضعیت تحویل فقط برای پیامکِ ارسال‌شده ثبت می‌شود.",

  // Reports
  "Reports.DateRangeRequired": "تاریخ شروع و پایان گزارش را انتخاب کنید.",
  "Reports.InvalidDateRange": "بازهٔ تاریخ نامعتبر است.",
  "Reports.RangeTooLong": "بازهٔ گزارش حداکثر یک سال (۳۶۶ روز) است.",

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
