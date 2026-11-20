import { StudentListPage } from "./StudentList";

/**
 * The trainer's «شاگردان غیرفعال» (BUSINESS_RULES.md §15 *The student's profile and every visit in
 * the month*, 15.7p): the students whose coaching period is over and was not renewed, opened from
 * their card under «شاگردان من» on the home, with their own search. A «تمدید» brings a student back
 * to «شاگردان من».
 */
export function InactiveStudentsPage() {
  return <StudentListPage part="inactive" />;
}
