namespace Gym.Domain.Payables;

/// <summary>
/// What the gym owes on a date (BUSINESS_RULES.md §9 <i>Cheques and instalments</i>). Stored by
/// name, so the column reads the same in a database console as on the screen.
/// </summary>
public enum PayableKind
{
    /// <summary>«چک»: a dated cheque the gym wrote. Paid only on or after its date.</summary>
    Cheque,

    /// <summary>«قسط»: one instalment, «قسط n از N». Paid at any time.</summary>
    Installment,
}
