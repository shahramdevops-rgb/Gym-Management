import { render, screen } from "@testing-library/react";

import { Table, TableBody, TableCell, TableRow } from "./table";

function renderRow(props: { label?: string; joins?: "below" | "above" | "both" }) {
  render(
    <Table>
      <TableBody>
        <TableRow joins={props.joins}>
          <TableCell label={props.label}>۲۵</TableCell>
        </TableRow>
      </TableBody>
    </Table>,
  );

  return { row: screen.getByRole("row"), cell: screen.getByRole("cell") };
}

/** The classes `after` has that `before` does not. */
function addedClasses(before: Element, after: Element): string[] {
  const had = new Set(before.classList);
  return [...after.classList].filter((name) => !had.has(name));
}

describe("Table", () => {
  it("TableCell_WithLabel_KeepsTheLabelOutOfTheCellsText", () => {
    const { cell } = renderRow({ label: "کمد" });

    expect(cell).toHaveAttribute("data-label", "کمد");
    expect(cell).toHaveTextContent(/^۲۵$/);
  });

  it("TableCell_WithoutLabel_HasNoLabel", () => {
    const { cell } = renderRow({});

    expect(cell).not.toHaveAttribute("data-label");
  });

  // The computer's table stays as it was (13.1b): whatever a label or a join changes is a
  // `max-lg:` rule, for a phone only.
  it.each(["below", "above", "both"] as const)("TableRow_Joins%s_ChangesThePhoneOnly", (joins) => {
    const plain = renderRow({});
    const plainRow = plain.row.cloneNode() as Element;
    const plainCell = plain.cell.cloneNode() as Element;
    document.body.innerHTML = "";

    const joined = renderRow({ label: "کمد", joins });

    const added = [...addedClasses(plainRow, joined.row), ...addedClasses(plainCell, joined.cell)];
    expect(added.length).toBeGreaterThan(0);
    for (const name of added) {
      expect(name).toMatch(/^max-lg:/);
    }
  });
});
