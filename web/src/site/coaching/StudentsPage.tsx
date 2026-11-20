import { StudentListPage } from "./StudentList";

/**
 * The trainer's «شاگردان من», a page of its own since 1405/08/20 (the developer: the home showed the
 * whole list; now a card on it with the count opens this page, lazy, so its code comes only when
 * opened). Since 15.7p the active students only (BUSINESS_RULES.md §15 *The student's profile and
 * every visit in the month*): the inactive ones have their own page, `InactiveStudentsPage`.
 */
export function StudentsPage() {
  return <StudentListPage part="active" />;
}
