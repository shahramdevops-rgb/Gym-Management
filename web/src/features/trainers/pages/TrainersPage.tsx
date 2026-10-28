import { Dumbbell, Plus } from "lucide-react";
import { useState } from "react";

import { PageHeader } from "@/components/PageHeader";
import { EmptyState, LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { toneAction } from "@/lib/edges";
import { errorMessage } from "@/lib/errors";

import { useSetTrainerActive, useTrainers, type Trainer } from "../api";
import { StopTrainerDialog } from "../components/StopTrainerDialog";
import { TrainerBox } from "../components/TrainerBox";
import { TrainerFormDialog } from "../components/TrainerFormDialog";

/**
 * «مربیان», Owner only (the route is wrapped in RequireRole): only the Owner makes someone a
 * trainer (BUSINESS_RULES.md §15 *Trainers*). Nothing is deleted: a trainer is stopped, and made
 * one again with a new period, the earlier ones kept with their dates.
 *
 * Stopping asks first; «شروع دوباره» does not, since it takes nothing away and is undone by
 * stopping again.
 */
export function TrainersPage() {
  const trainers = useTrainers();
  const setActive = useSetTrainerActive();
  // `null` closed, `"new"` adding, a trainer while editing them.
  const [editing, setEditing] = useState<Trainer | "new" | null>(null);
  const [stopping, setStopping] = useState<Trainer | null>(null);
  const [notice, setNotice] = useState<{ kind: "success" | "destructive"; text: string } | null>(
    null,
  );

  async function changeActive(trainer: Trainer, active: boolean) {
    setNotice(null);
    try {
      await setActive.mutateAsync({ id: trainer.id, active });
      setNotice({
        kind: "success",
        text: active
          ? `«${trainer.fullName}» دوباره مربی شد.`
          : `مربی‌گری «${trainer.fullName}» تمام شد.`,
      });
    } catch (problem) {
      setNotice({ kind: "destructive", text: errorMessage(problem) });
    } finally {
      setStopping(null);
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="مربیان"
        description="مربی‌ها با شماره موبایلی که این‌جا ثبت می‌شود وارد سایت می‌شوند."
        actions={
          <Button variant="ghost" className={toneAction} onClick={() => setEditing("new")}>
            <Plus aria-hidden />
            افزودن مربی
          </Button>
        }
      />

      {notice !== null && (
        <Alert variant={notice.kind} role={notice.kind === "success" ? "status" : "alert"}>
          {notice.text}
        </Alert>
      )}

      {trainers.isPending && <LoadingState />}
      {trainers.isError && <Alert variant="destructive">{errorMessage(trainers.error)}</Alert>}

      {trainers.isSuccess && trainers.data.length === 0 && (
        <EmptyState icon={Dumbbell}>هنوز هیچ مربی‌ای ثبت نشده است.</EmptyState>
      )}

      {trainers.isSuccess && trainers.data.length > 0 && (
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {trainers.data.map((trainer) => (
            <TrainerBox
              key={trainer.id}
              trainer={trainer}
              busy={setActive.isPending}
              onEdit={() => setEditing(trainer)}
              onStop={() => setStopping(trainer)}
              onRestart={() => void changeActive(trainer, true)}
            />
          ))}
        </div>
      )}

      <TrainerFormDialog
        open={editing !== null}
        trainer={editing === "new" || editing === null ? undefined : editing}
        onClose={() => setEditing(null)}
      />

      <StopTrainerDialog
        trainer={stopping}
        pending={setActive.isPending}
        onConfirm={() => stopping !== null && void changeActive(stopping, false)}
        onCancel={() => setStopping(null)}
      />
    </div>
  );
}
