/** The site app's addresses (roadmap 15.3). Few, and none shared with the panel's. */
export const sitePaths = {
  home: "/",
  signIn: "/sign-in",
  signUp: "/sign-up",
  /** The member picks a trainer (15.4). */
  trainers: "/trainers",
  /** The trainer's «تمام سانس‌های من» (15.6a). */
  timetable: "/timetable",
  /** The trainer's «برنامهٔ ثابت» of one student (15.6b). */
  student: (memberId: string) => `/students/${memberId}`,
  studentPattern: "/students/:memberId",
  /** The member's «سانس‌های من» (15.6b). */
  mySessions: "/sessions",
  /** The trainer's «حضور امروز» (15.7). */
  today: "/today",
  /** The trainer's «حضور» of one student, by Jalali month (15.7). */
  studentAttendance: (memberId: string) => `/students/${memberId}/attendance`,
  studentAttendancePattern: "/students/:memberId/attendance",
  /** The member's «حضور من», by Jalali month (15.7j). */
  myAttendance: "/my-attendance",
} as const;

/**
 * The gym's public page, linked at the foot of the home (asked by the developer, 2026-11-03).
 * Its own host on the server, the address its canonical link names; in development the copy
 * `npm run dev:site` serves beside the app (web/vite.site.config.ts).
 */
export const gymSiteAddress = import.meta.env.PROD
  ? "https://pasargadgymplus.ir/"
  : "/public-site/";
