import { groupBySettlement } from "./settlementGroups";

describe("groupBySettlement", () => {
  const first = { id: "s1", total: 300, itemCount: 3 };
  const second = { id: "s2", total: 50, itemCount: 2 };

  function row(name: string, settlement: typeof first | null = null) {
    return { name, settlement };
  }

  it("groups neighbouring rows of one settlement and leaves the rest alone", () => {
    const groups = groupBySettlement([
      row("a"),
      row("b", first),
      row("c", first),
      row("d", first),
      row("e"),
    ]);

    expect(groups).toEqual([
      { kind: "single", item: row("a") },
      {
        kind: "settlement",
        settlement: first,
        items: [row("b", first), row("c", first), row("d", first)],
      },
      { kind: "single", item: row("e") },
    ]);
  });

  it("keeps two settlements apart even when they are neighbours", () => {
    const groups = groupBySettlement([
      row("a", first),
      row("b", first),
      row("c", second),
      row("d", second),
    ]);

    expect(groups.map((group) => group.kind === "settlement" && group.settlement.id)).toEqual([
      "s1",
      "s2",
    ]);
  });

  it("leaves a settlement with one row on the page as a plain row", () => {
    expect(groupBySettlement([row("a", first), row("b")])).toEqual([
      { kind: "single", item: row("a", first) },
      { kind: "single", item: row("b") },
    ]);
  });
});
