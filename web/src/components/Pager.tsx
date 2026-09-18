import { Button } from "@/components/ui/button";
import { toPersianDigits } from "@/lib/format";

interface PagerProps {
  page: number;
  pageCount: number;
  onPageChange: (page: number) => void;
}

/** Previous / next with "page N of M" in Persian digits. Hidden when everything fits on one page. */
export function Pager({ page, pageCount, onPageChange }: PagerProps) {
  if (pageCount <= 1) {
    return null;
  }

  return (
    <nav aria-label="صفحه‌بندی" className="flex items-center gap-3">
      <Button
        size="sm"
        variant="outline"
        disabled={page <= 1}
        onClick={() => onPageChange(page - 1)}
      >
        قبلی
      </Button>
      <span className="text-sm text-muted-foreground">
        صفحهٔ {toPersianDigits(page)} از {toPersianDigits(pageCount)}
      </span>
      <Button
        size="sm"
        variant="outline"
        disabled={page >= pageCount}
        onClick={() => onPageChange(page + 1)}
      >
        بعدی
      </Button>
    </nav>
  );
}
