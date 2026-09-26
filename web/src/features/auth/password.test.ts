import { isTooSimple, looksLikePersianKeyboard, passwordChecks, passwordProblem } from "./password";

describe("passwordProblem", () => {
  it.each(["tabriz lamp 4 kettle", "TabrizLamp4Kettle", "mX9#vQ2!rT7p", "lamps and kettles"])(
    "PasswordProblem_LongUncommonPassword_ReturnsUndefined (%s)",
    (password) => {
      expect(passwordProblem(password, "reza")).toBeUndefined();
    },
  );

  it("PasswordProblem_ElevenCharacters_SaysTwelveAreNeeded", () => {
    expect(passwordProblem("mX9#vQ2!rT7")).toBe("رمز عبور باید دست‌کم ۱۲ نویسه باشد.");
  });

  it("PasswordProblem_PersianDigits_CountAsEnglishDigits", () => {
    expect(passwordProblem("tabriz lamp ۴ kettle")).toBeUndefined();
  });

  it("PasswordProblem_PersianLetters_AreRefusedBeforeTheLength", () => {
    expect(passwordProblem("رمز")).toMatch(/انگلیسی/);
  });

  it("PasswordProblem_ContainsTheUserNameInAnyCase_IsRefused", () => {
    expect(passwordProblem("REZA lamp 4 kettle", "reza")).toBe(
      "نام کاربری نباید داخل رمز عبور باشد.",
    );
  });
});

describe("isTooSimple", () => {
  it.each([
    "aaaaaaaaaaaa",
    "abababababab",
    "abcdabcdabcd",
    "123456789012",
    "210987654321",
    "QWERTYUIOPASD",
  ])("IsTooSimple_RepetitionOrSequence_ReturnsTrue (%s)", (password) => {
    expect(isTooSimple(password)).toBe(true);
  });

  it("IsTooSimple_Passphrase_ReturnsFalse", () => {
    expect(isTooSimple("kettle under the lamp")).toBe(false);
  });
});

describe("looksLikePersianKeyboard", () => {
  it("LooksLikePersianKeyboard_PersianDigitsOnly_ReturnsFalse", () => {
    expect(looksLikePersianKeyboard("۱۲۳۴")).toBe(false);
  });

  it("LooksLikePersianKeyboard_PersianLetter_ReturnsTrue", () => {
    expect(looksLikePersianKeyboard("abc ش")).toBe(true);
  });
});

describe("passwordChecks", () => {
  it("PasswordChecks_NoUserName_LeavesOutTheUserNameLine", () => {
    expect(passwordChecks("anything").map((check) => check.key)).toEqual([
      "length",
      "english",
      "simple",
    ]);
  });

  it("PasswordChecks_GoodPassword_MeetsEveryLine", () => {
    expect(passwordChecks("tabriz lamp 4 kettle", "reza").every((check) => check.met)).toBe(true);
  });

  it("PasswordChecks_Empty_MeetsNothing", () => {
    expect(passwordChecks("", "reza").some((check) => check.met)).toBe(false);
  });
});
