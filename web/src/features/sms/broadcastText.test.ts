import {
  broadcastMaxTextLength,
  broadcastMessage,
  normalizeBroadcastText,
  smsParts,
} from "./broadcastText";

/** The same cases as `SmsPartsTests` and `SmsBroadcastTests` in the backend. */
describe("broadcastText", () => {
  it.each([
    [0, 1],
    [1, 1],
    [70, 1],
    [71, 2],
    [134, 2],
    [135, 3],
    [201, 3],
    [202, 4],
  ])("smsParts_%i characters_IsTheNumberOfParts", (length, expected) => {
    expect(smsParts("ب".repeat(length))).toBe(expected);
  });

  it("smsParts_LineBreak_IsOneCharacter", () => {
    expect(smsParts(`${"ب".repeat(69)}\n`)).toBe(1);
  });

  it("smsParts_Emoji_TakesTwoCharacters", () => {
    expect(smsParts(`${"ب".repeat(69)}😀`)).toBe(2);
  });

  it("normalizeBroadcastText_BrowserLineBreaks_AreOneCharacterAndTheEndsTrimmed", () => {
    expect(normalizeBroadcastText("  خط اول\r\nخط دوم\rخط سوم  ")).toBe("خط اول\nخط دوم\nخط سوم");
  });

  it("broadcastMessage_FirstNameAndText_IsWhatThePhoneGets", () => {
    expect(broadcastMessage("محمد", " فردا تعطیل است. ")).toBe("محمد عزیز، فردا تعطیل است.\nلغو11");
  });

  it("broadcastMaxTextLength_ThreeFullPartsWithTheName_Is185ForAli", () => {
    // «علی عزیز، » is 10 characters and «\nلغو11» 6: 185 more make 201, three full parts.
    expect(broadcastMaxTextLength("علی")).toBe(185);
    expect(smsParts(broadcastMessage("علی", "ب".repeat(185)))).toBe(3);
    expect(smsParts(broadcastMessage("علی", "ب".repeat(186)))).toBe(4);
  });
});
