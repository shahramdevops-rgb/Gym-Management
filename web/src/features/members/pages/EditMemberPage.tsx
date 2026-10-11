import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router";

import { paths } from "@/app/paths";
import { LoadingState } from "@/components/States";
import { PageHeader } from "@/components/PageHeader";
import { StaleAlert } from "@/components/SaveRow";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";

import { useMember, useUpdateMember } from "../api";
import { MemberForm } from "../components/MemberForm";

const changedConcurrently = "Members.ChangedConcurrently";

function isCode(problem: unknown, code: string): boolean {
  return (
    typeof problem === "object" && problem !== null && "code" in problem && problem.code === code
  );
}

/**
 * Edits a member, active or not (docs/BUSINESS_RULES.md §2).
 *
 * The form is filled from one `version` of the member and sends that version back. If a
 * colleague saved in the meantime the API refuses, and this page says so and offers to load
 * their version, rather than quietly overwriting it.
 */
export function EditMemberPage() {
  const { id = "" } = useParams();
  const member = useMember(id);
  const updateMember = useUpdateMember();
  const navigate = useNavigate();
  const [stale, setStale] = useState(false);
  const [reloading, setReloading] = useState(false);

  if (member.isPending) {
    return <LoadingState />;
  }

  if (member.isError) {
    return <Alert variant="destructive">{errorMessage(member.error)}</Alert>;
  }

  const current = member.data;

  async function reload() {
    setReloading(true);
    await member.refetch();
    setReloading(false);
    setStale(false);
  }

  return (
    <div className="max-w-2xl space-y-6">
      <PageHeader title={<>ویرایش «{current.fullName}»</>} />
      <Card>
        <CardContent className="space-y-4">
          {/*
          Keyed by version: once the fresh member is loaded, React builds a new form with its
          values, instead of keeping the fields filled from the old one.
        */}
          <MemberForm
            key={String(current.version)}
            defaultValues={{
              fullName: current.fullName,
              phoneNumber: current.phoneNumber,
              birthDate: current.birthDate,
              notes: current.notes ?? "",
            }}
            submitLabel="ذخیره"
            submittingLabel="در حال ذخیره…"
            editing
            stale={
              stale ? (
                <StaleAlert
                  code={changedConcurrently}
                  reloading={reloading}
                  onReload={() => void reload()}
                />
              ) : undefined
            }
            onSubmit={async (input) => {
              try {
                await updateMember.mutateAsync({ id, version: current.version, ...input });
                navigate(paths.member(id));
              } catch (problem) {
                if (!isCode(problem, changedConcurrently)) {
                  throw problem;
                }
                setStale(true);
              }
            }}
            actions={
              <Button asChild variant="ghost">
                <Link to={paths.member(id)}>انصراف</Link>
              </Button>
            }
          />
        </CardContent>
      </Card>
    </div>
  );
}
