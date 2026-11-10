import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { formatDate } from "@/lib/format";

import {
  useMyCoachingNews,
  useSeeMyCoachingNews,
  useSeeStudentNews,
  useStudentNews,
  type CoachingNewsItem,
} from "./api";

/** The card's sentence for each kind; the name is the trainer's on a member's card, the student's on a trainer's. */
function newsSentence(item: CoachingNewsItem): string {
  switch (item.kind) {
    case "Accepted":
      return `${item.fullName} درخواست شما را پذیرفت و مربی شما شد.`;
    case "Declined":
      return `${item.fullName} درخواست شما را نپذیرفت.`;
    case "EndedByTrainer":
      return `${item.fullName} شما را از شاگردانش برداشت.`;
    case "EndedByStudent":
      return `${item.fullName} مربی‌گری شما را تمام کرد.`;
  }
}

interface NewsCardProps {
  title: string;
  items: CoachingNewsItem[];
  error: unknown;
  pending: boolean;
  onSeen: (ids: string[]) => void;
}

/**
 * One piece of news a line, newest first, with its day; one «دیدم» for all that the card shows, so
 * news that comes after stays. Nothing shows while it loads, when there is nothing to tell, or when
 * it cannot be read.
 */
function NewsCard({ title, items, error, pending, onSeen }: NewsCardProps) {
  if (items.length === 0) {
    return null;
  }

  return (
    <Card accent={false} className="gap-3 border-primary/30 shadow-raised">
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <ul aria-label={title} className="space-y-2">
          {items.map((item) => (
            <li key={item.id} className="space-y-1 rounded-lg border p-2">
              <p className="text-sm">{newsSentence(item)}</p>
              <p className="text-xs text-muted-foreground">{formatDate(item.on)}</p>
            </li>
          ))}
        </ul>
        {error !== null && <Alert variant="destructive">{errorMessage(error)}</Alert>}
        <Button
          variant="outline"
          className="w-full"
          disabled={pending}
          onClick={() => onSeen(items.map((item) => item.id))}
        >
          دیدم
        </Button>
      </CardContent>
    </Card>
  );
}

/**
 * «خبر مربی» at the top of an active member's home (BUSINESS_RULES.md §15 *A member and their
 * trainer*, roadmap 15.4d): the trainer accepted or declined their request, or ended the link. In
 * the app only, no SMS; until «دیدم», 30 days at most.
 */
export function MyCoachingNewsCard() {
  const news = useMyCoachingNews();
  const see = useSeeMyCoachingNews();

  return (
    <NewsCard
      title="خبر مربی"
      items={news.data?.items ?? []}
      error={see.error}
      pending={see.isPending}
      onSeen={(ids) => see.mutate(ids)}
    />
  );
}

/**
 * «خبر شاگردان» at the top of a trainer's area (roadmap 15.4d): the students who ended their link
 * themselves. Nobody is told when another trainer takes a student over.
 */
export function StudentNewsCard() {
  const news = useStudentNews();
  const see = useSeeStudentNews();

  return (
    <NewsCard
      title="خبر شاگردان"
      items={news.data?.items ?? []}
      error={see.error}
      pending={see.isPending}
      onSeen={(ids) => see.mutate(ids)}
    />
  );
}
