import { errorMessage, errorMessages, fieldErrors, networkErrorMessage } from "./errors";

describe("errorMessage", () => {
  it("ErrorMessage_KnownCode_ReturnsPersianMessage", () => {
    expect(errorMessage({ code: "Auth.InvalidCredentials" })).toBe(
      "نام کاربری یا رمز عبور اشتباه است.",
    );
  });

  it("ErrorMessage_UnknownCode_ReturnsGenericPersianMessageWithCorrelationId", () => {
    const message = errorMessage({ code: "Something.New", correlationId: "abc123" });

    expect(message).toContain(errorMessages["General.Unexpected"]);
    expect(message).toContain("abc123");
  });

  it("ErrorMessage_NetworkFailure_SaysTheServerCouldNotBeReached", () => {
    expect(errorMessage(new TypeError("Failed to fetch"))).toBe(networkErrorMessage);
  });

  it("ErrorMessages_EveryMessage_IsPersianNotEnglish", () => {
    // A Persian UI: no message may be (or fall back to) English text.
    for (const [code, message] of Object.entries(errorMessages)) {
      expect(message, code).toMatch(/[؀-ۿ]/);
      expect(message, code).not.toMatch(/[A-Za-z]{3,}/);
    }
  });
});

describe("fieldErrors", () => {
  it("FieldErrors_ValidationProblem_MapsFirstCodeOfEachFieldToPersian", () => {
    const errors = fieldErrors({
      code: "General.ValidationFailed",
      errors: {
        userName: [
          { code: "Staff.UserNameLength", description: "User name must be 3 to 50 characters." },
        ],
        temporaryPassword: [
          { code: "Auth.PasswordTooShort", description: "Too short." },
          { code: "Auth.PasswordRequiresLetterAndDigit", description: "Letter and digit." },
        ],
      },
    });

    expect(errors).toEqual({
      userName: "نام کاربری باید بین ۳ تا ۵۰ نویسه باشد.",
      temporaryPassword: "رمز عبور باید دست‌کم ۸ نویسه باشد.",
    });
  });

  it("FieldErrors_NoErrorsProperty_ReturnsEmpty", () => {
    expect(fieldErrors({ code: "Auth.InvalidCredentials" })).toEqual({});
  });
});
