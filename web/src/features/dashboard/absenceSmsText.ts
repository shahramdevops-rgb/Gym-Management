import { toPersianDigits } from "@/lib/format";

import { reportThresholds } from "./api";

/** The window, in Persian digits: «۵». */
export const absenceSmsWindowDays = toPersianDigits(reportThresholds.absenceSmsWindowDays);

/**
 * Why a message is not measured, in one line, the same on the card and in «نیاز به اقدام» (§12
 * *The absence SMS's effect*).
 */
export const notMeasurableRule = `پلن عضو در آن ${absenceSmsWindowDays} روز تمام می‌شد، لغو شد یا فریز شد؛ در درصد حساب نمی‌شود.`;
