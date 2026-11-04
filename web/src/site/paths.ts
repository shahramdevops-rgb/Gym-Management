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
} as const;
