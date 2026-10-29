import { useMutation, useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query";

import type { components } from "@/lib/api/schema";

import { siteApi } from "../api/client";
import { siteMeQueryKey } from "../auth/api";

export type TrainerChoice = components["schemas"]["TrainerChoiceResponse"];
export type MyTrainer = components["schemas"]["MyTrainerResponse"];
export type Students = components["schemas"]["StudentsResponse"];

export const trainerChoicesQueryKey = ["site", "coaching", "trainers"] as const;
export const myTrainerQueryKey = ["site", "coaching", "my-trainer"] as const;
export const studentsQueryKey = ["site", "coaching", "students"] as const;

/**
 * A refused change means the other side, the desk or the Owner changed something first: a request
 * answered meanwhile, a member deactivated, a trainer stopped. Both the list and «me» are read
 * again, so the screen shows what is true now under the error's message.
 */
function refreshAfterFailure(queryClient: QueryClient, key: readonly string[]) {
  void queryClient.invalidateQueries({ queryKey: key });
  void queryClient.invalidateQueries({ queryKey: siteMeQueryKey });
}

// ---- The member (BUSINESS_RULES.md §15 *A member and their trainer*) ----

/** The trainers a member can pick, by name only until 14.1's photos. */
export function useTrainerChoices() {
  return useQuery({
    queryKey: trainerChoicesQueryKey,
    queryFn: async () => {
      const { data, error } = await siteApi.GET("/api/site/coaching/trainers");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** «مربی من»: the trainer now and the request waiting, either, both or neither. */
export function useMyTrainer() {
  return useQuery({
    queryKey: myTrainerQueryKey,
    refetchOnWindowFocus: true,
    queryFn: async () => {
      const { data, error } = await siteApi.GET("/api/site/coaching/my-trainer");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** Every member action answers with «مربی من» as it is after it, which replaces the cached one. */
function useMyTrainerAction<TVariables>(send: (variables: TVariables) => Promise<MyTrainer>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: send,
    onSuccess: (myTrainer) => queryClient.setQueryData(myTrainerQueryKey, myTrainer),
    onError: () => refreshAfterFailure(queryClient, myTrainerQueryKey),
  });
}

/** «انتخاب این مربی»: a new request, which replaces one already waiting. */
export function useRequestTrainer() {
  return useMyTrainerAction(async (trainerId: string) => {
    const { data, error } = await siteApi.POST("/api/site/coaching/my-trainer/request", {
      body: { trainerId },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

/** «لغو» on the request waiting for an answer. */
export function useCancelTrainerRequest() {
  return useMyTrainerAction(async () => {
    const { data, error } = await siteApi.POST("/api/site/coaching/my-trainer/cancel-request");
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

/** «پایان مربی‌گری»: the member ends the link, from today. */
export function useEndMyTrainer() {
  return useMyTrainerAction(async () => {
    const { data, error } = await siteApi.POST("/api/site/coaching/my-trainer/end");
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

// ---- The trainer ----

/** The requests waiting for this trainer, and «شاگردان من». */
export function useStudents() {
  return useQuery({
    queryKey: studentsQueryKey,
    refetchOnWindowFocus: true,
    queryFn: async () => {
      const { data, error } = await siteApi.GET("/api/site/coaching/students");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** Every trainer action answers with the requests and students as they are after it. */
function useStudentsAction(send: (id: string) => Promise<Students>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: send,
    onSuccess: (students) => queryClient.setQueryData(studentsQueryKey, students),
    onError: () => refreshAfterFailure(queryClient, studentsQueryKey),
  });
}

/** «پذیرفتن»: the member becomes this trainer's student, and leaves their former trainer today. */
export function useAcceptTrainerRequest() {
  return useStudentsAction(async (requestId) => {
    const { data, error } = await siteApi.POST("/api/site/coaching/requests/{id}/accept", {
      params: { path: { id: requestId } },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

/** «رد کردن»: the member may ask again, this trainer too. */
export function useDeclineTrainerRequest() {
  return useStudentsAction(async (requestId) => {
    const { data, error } = await siteApi.POST("/api/site/coaching/requests/{id}/decline", {
      params: { path: { id: requestId } },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

/** «پایان» for one student, from today. */
export function useEndStudent() {
  return useStudentsAction(async (memberId) => {
    const { data, error } = await siteApi.POST("/api/site/coaching/students/{memberId}/end", {
      params: { path: { memberId } },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}
