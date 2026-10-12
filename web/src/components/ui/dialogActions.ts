import { cn } from "@/lib/utils";

/**
 * The class for a form's row of buttons: inside a dialog that fills a phone's screen
 * (`DialogContent`'s `fullScreenOnPhone`, which marks itself `data-phone-full`), the row stays at
 * the bottom of the screen while the form scrolls under it, so «ثبت» is never a screen away
 * (13.1c), and it sits at the bottom of a short form too. Anywhere else (a page, a confirmation's
 * card, the computer) it does nothing, so a form shared with a page can always carry it. The row
 * is put last on the phone, after anything the form adds below it. It holds where the row's parent
 * is as tall as the form: a direct child of `DialogContent`, or the last row of a form's body.
 *
 * Its own file, not `dialog.tsx`: a file of components that also exports a constant loses React's
 * fast refresh.
 */
export const dialogActionsClassName = cn(
  "max-lg:in-data-phone-full:sticky max-lg:in-data-phone-full:bottom-0 max-lg:in-data-phone-full:z-10",
  "max-lg:in-data-phone-full:order-last max-lg:in-data-phone-full:mt-auto max-lg:in-data-phone-full:shrink-0",
  // Edge to edge however its parent lays it out: a block, a column, or a row it then fills alone.
  "max-lg:in-data-phone-full:w-[calc(100%+2rem)]",
  "max-lg:in-data-phone-full:-mx-4 max-lg:in-data-phone-full:-mb-4 max-lg:in-data-phone-full:border-t",
  "max-lg:in-data-phone-full:bg-card max-lg:in-data-phone-full:px-4 max-lg:in-data-phone-full:py-3",
);
