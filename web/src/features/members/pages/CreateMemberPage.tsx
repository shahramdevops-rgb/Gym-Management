import { Link, useNavigate } from "react-router";

import { paths } from "@/app/paths";
import { PageHero } from "@/components/PageHero";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

import { useCreateMember } from "../api";
import { MemberForm } from "../components/MemberForm";

/** After saving, the new member's profile opens: the next step is usually selling a plan there. */
export function CreateMemberPage() {
  const createMember = useCreateMember();
  const navigate = useNavigate();

  return (
    <div className="max-w-2xl space-y-6">
      {/* The banner and an edged card, like the members' list (13.2j). */}
      <PageHero title="عضو جدید" />
      <Card edge>
        <CardContent>
          <MemberForm
            submitLabel="ثبت عضو"
            submittingLabel="در حال ثبت…"
            onSubmit={async (input) => {
              const member = await createMember.mutateAsync(input);
              navigate(paths.member(member.id));
            }}
            actions={
              <Button asChild variant="ghost">
                <Link to={paths.members}>انصراف</Link>
              </Button>
            }
          />
        </CardContent>
      </Card>
    </div>
  );
}
