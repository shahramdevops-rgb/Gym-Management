import { ImagePlus, ImageOff } from "lucide-react";
import { useId, useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

import {
  photoUrl,
  useAddShopPhoto,
  useRemoveShopPhoto,
  useReorderShopPhotos,
  type ShopProduct,
} from "../api";
import { catalogLimits } from "../schemas";

/** The API's limit (IImageProcessor.MaxUploadBytes), checked here too so a big file is refused at once. */
const maxBytes = 10 * 1024 * 1024;

/** The look of the three buttons under a photo: short, filling their column. */
const photoAction = "h-9 w-full px-1 text-xs";

/**
 * A product's photos (BUSINESS_RULES.md §16 *Products*): up to eight, the first is the main one. On
 * a phone «افزودن عکس» offers the camera or the gallery (the browser's own choice for a file input);
 * several may be picked at once and go up one by one. The server re-encodes each and drops its
 * EXIF data (where it was taken). Each photo is shown whole, never cut.
 */
export function ProductPhotos({ product, canEdit }: { product: ShopProduct; canEdit: boolean }) {
  const add = useAddShopPhoto(product.id);
  const remove = useRemoveShopPhoto(product.id);
  const reorder = useReorderShopPhotos(product.id);
  const inputId = useId();
  const [progress, setProgress] = useState<{ done: number; total: number } | null>(null);
  const [problem, setProblem] = useState<string | null>(null);

  const photos = product.photos;
  const room = catalogLimits.photos - photos.length;
  const busy = progress !== null || remove.isPending || reorder.isPending;

  async function upload(files: File[]) {
    setProblem(null);
    const chosen = files.slice(0, room);
    const tooBig = chosen.filter((file) => file.size > maxBytes);
    const sent = chosen.filter((file) => file.size <= maxBytes);
    const problems: string[] = [];
    if (files.length > room) {
      problems.push(`فقط ${toPersianDigits(room)} عکس دیگر جا دارد؛ بقیه فرستاده نشد.`);
    }
    if (tooBig.length > 0) {
      problems.push(
        `${tooBig.map((file) => file.name).join("، ")}: ${errorMessage({ code: "Photos.TooLarge" })}`,
      );
    }

    setProgress({ done: 0, total: sent.length });
    for (const [index, file] of sent.entries()) {
      try {
        await add.mutateAsync(file);
      } catch (failure) {
        problems.push(`${file.name}: ${errorMessage(failure)}`);
      }
      setProgress({ done: index + 1, total: sent.length });
    }
    setProgress(null);
    setProblem(problems.length > 0 ? problems.join(" ") : null);
  }

  function move(from: number, to: number) {
    const ids = photos.map((photo) => photo.id);
    const [moved] = ids.splice(from, 1);
    if (moved === undefined) {
      return;
    }
    ids.splice(to, 0, moved);
    setProblem(null);
    reorder.mutate(ids, { onError: (failure) => setProblem(errorMessage(failure)) });
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          عکس‌ها{" "}
          <span className="text-sm font-normal text-muted-foreground">
            ({toPersianDigits(photos.length)} از {toPersianDigits(catalogLimits.photos)})
          </span>
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {photos.length === 0 && (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <ImageOff aria-hidden className="size-4" />
            هنوز عکسی نیست{canEdit && "؛ کالای بی‌عکس در سایت کمتر دیده می‌شود"}.
          </div>
        )}

        {photos.length > 0 && (
          <ul className="grid grid-cols-2 gap-3 sm:grid-cols-4" aria-label="عکس‌های کالا">
            {photos.map((photo, index) => (
              <li key={photo.id} className="space-y-2">
                <a
                  href={photoUrl(photo.id, "large")}
                  target="_blank"
                  rel="noreferrer"
                  className="relative grid aspect-square place-items-center overflow-hidden rounded-lg border bg-muted"
                >
                  <img
                    src={photoUrl(photo.id, "small")}
                    alt={`عکس ${toPersianDigits(index + 1)} از ${product.name}`}
                    width={Number(photo.width)}
                    height={Number(photo.height)}
                    loading="lazy"
                    className="size-full object-contain"
                  />
                  {index === 0 && <Badge className="absolute start-1.5 top-1.5">اصلی</Badge>}
                </a>
                {canEdit && (
                  // Three short buttons in one row under the photo, the same width on a phone's
                  // narrow column and on the computer; «حذف» always in the last place.
                  <div className="grid grid-cols-3 gap-1">
                    {index > 0 && (
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        className={photoAction}
                        disabled={busy}
                        aria-label={`عکس ${toPersianDigits(index + 1)} اصلی شود`}
                        onClick={() => move(index, 0)}
                      >
                        اصلی
                      </Button>
                    )}
                    {index > 0 && (
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        className={photoAction}
                        disabled={busy}
                        aria-label={`عکس ${toPersianDigits(index + 1)} یکی جلوتر`}
                        onClick={() => move(index, index - 1)}
                      >
                        جلوتر
                      </Button>
                    )}
                    <Button
                      type="button"
                      size="sm"
                      variant="ghost"
                      className={cn(
                        photoAction,
                        "col-start-3 text-destructive hover:bg-destructive/10 hover:text-destructive",
                      )}
                      disabled={busy}
                      aria-label={`حذف عکس ${toPersianDigits(index + 1)}`}
                      onClick={() => {
                        setProblem(null);
                        remove.mutate(photo.id, {
                          onError: (failure) => setProblem(errorMessage(failure)),
                        });
                      }}
                    >
                      حذف
                    </Button>
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}

        {canEdit && room > 0 && (
          <div>
            <input
              id={inputId}
              type="file"
              accept="image/jpeg,image/png,image/webp"
              multiple
              className="sr-only"
              disabled={busy}
              onChange={(event) => {
                const files = Array.from(event.target.files ?? []);
                event.target.value = "";
                if (files.length > 0) {
                  void upload(files);
                }
              }}
            />
            <label
              htmlFor={inputId}
              className={cn(
                "inline-flex min-h-11 cursor-pointer items-center gap-2 rounded-md border px-4 text-sm font-medium hover:bg-accent",
                busy && "pointer-events-none opacity-50",
              )}
            >
              <ImagePlus aria-hidden className="size-4" />
              افزودن عکس
            </label>
            <p className="mt-2 text-xs text-muted-foreground">
              عکس معمولی گوشی یا کامپیوتر، هر کدام تا ۱۰ مگابایت. اولین عکس، عکس اصلی است.
            </p>
          </div>
        )}

        {progress !== null && (
          <p className="text-sm" role="status">
            در حال فرستادن عکس {toPersianDigits(Math.min(progress.done + 1, progress.total))} از{" "}
            {toPersianDigits(progress.total)}…
          </p>
        )}
        {problem !== null && <Alert variant="destructive">{problem}</Alert>}
      </CardContent>
    </Card>
  );
}
