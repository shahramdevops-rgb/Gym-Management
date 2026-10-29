import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

/** A trainer as «مربیان» shows them, with every period oldest first (BUSINESS_RULES.md §15). */
export type Trainer = components["schemas"]["TrainerResponse"];

export type TrainerPeriod = components["schemas"]["TrainerPeriodResponse"];

export interface TrainerInput {
  fullName: string;
  phoneNumber: string;
}

const trainersQueryKey = ["trainers"] as const;

/** Every trainer, the active ones first: a gym has a handful, so there are no pages. */
export function useTrainers() {
  return useQuery({
    queryKey: trainersQueryKey,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/trainers");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** Every change to a trainer refreshes the list, so a box never shows stale state. */
function useTrainerMutation<TArgs>(request: (args: TArgs) => Promise<Trainer>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: request,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: trainersQueryKey }),
  });
}

export function useCreateTrainer() {
  return useTrainerMutation(async (body: TrainerInput) => {
    const { data, error } = await api.POST("/api/trainers", { body });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

/** Sends the version the form was opened with, so an edit over someone else's is refused. */
export function useUpdateTrainer() {
  return useTrainerMutation(
    async ({
      id,
      version,
      ...input
    }: TrainerInput & { id: string; version: Trainer["version"] }) => {
      const { data, error } = await api.PUT("/api/trainers/{id}", {
        params: { path: { id } },
        body: { ...input, version },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  );
}

/** «پایان مربی‌گری» or «شروع دوباره»: the open period ends today, or a new one starts today. */
export function useSetTrainerActive() {
  return useTrainerMutation(async ({ id, active }: { id: string; active: boolean }) => {
    const options = { params: { path: { id } } };
    const { data, error } = active
      ? await api.POST("/api/trainers/{id}/restart", options)
      : await api.POST("/api/trainers/{id}/stop", options);
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}
