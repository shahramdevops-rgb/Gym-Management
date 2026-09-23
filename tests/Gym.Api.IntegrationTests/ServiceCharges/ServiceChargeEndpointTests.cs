using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Members.GetMemberDebt;
using Gym.Application.Payments;
using Gym.Application.ServiceCharges;
using Gym.Domain.Members;
using Gym.Domain.Payments;
using Gym.Domain.Plans;
using Gym.Domain.ServiceCharges;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.ServiceCharges;

/// <summary>
/// The هوازی charge end to end (BUSINESS_RULES.md §7 <i>Gym services</i>, §5 <i>Member debt</i>):
/// recording it against an open visit, correcting it while nothing is settled, voiding it with a
/// reason afterwards, and the money it puts on the member's account.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ServiceChargeEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    // ---- Recording ----

    [Fact]
    public async Task Record_OpenVisit_CreatesTheChargeAndLeavesItEditable()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);

        using var response = await RecordAsync(client, token, visit.Id, 10_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var charge = await ReadChargeAsync(response);
        charge.AttendanceId.ShouldBe(visit.Id);
        charge.MemberId.ShouldBe(visit.MemberId);
        charge.Kind.ShouldBe(ServiceChargeKind.Cardio);
        charge.Amount.ShouldBe(10_000m);
        charge.NetPaid.ShouldBe(0m);
        charge.PaymentStatus.ShouldBe(PaymentStatus.Unpaid);
        charge.CanChangeAmount.ShouldBeTrue();
        charge.VoidedAt.ShouldBeNull();
    }

    /// <summary>
    /// BUSINESS_RULES.md §7: one non-voided charge per visit per kind. The handler checks it and
    /// the partial unique index backs it up.
    /// </summary>
    [Fact]
    public async Task Record_SecondLiveChargeForTheSameVisit_Returns409ServiceChargesAlreadyCharged()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        await RecordOkAsync(client, token, visit.Id, 10_000m);

        using var response = await RecordAsync(client, token, visit.Id, 20_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("ServiceCharges.AlreadyCharged");
    }

    /// <summary>A voided charge is history, so the replacement the rule expects is allowed (§7).</summary>
    [Fact]
    public async Task Record_AfterTheFirstOneWasVoided_Succeeds()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var first = await RecordOkAsync(client, token, visit.Id, 10_000m);
        await VoidOkAsync(client, token, first.Id, "مبلغ اشتباه بود");

        using var response = await RecordAsync(client, token, visit.Id, 20_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ReadChargeAsync(response)).Amount.ShouldBe(20_000m);
    }

    [Fact]
    public async Task Record_ClosedVisit_Returns422ServiceChargesVisitNotOpen()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        await CheckOutOkAsync(client, token, visit.Id);

        using var response = await RecordAsync(client, token, visit.Id, 10_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("ServiceCharges.VisitNotOpen");
    }

    /// <summary>
    /// A cancelled check-in is a visit that never happened, and it is closed by the same column
    /// a check-out sets, so nothing can be charged to it either (§7).
    /// </summary>
    [Fact]
    public async Task Record_CancelledVisit_Returns422ServiceChargesVisitNotOpen()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        await CancelCheckInOkAsync(client, token, visit.Id);

        using var response = await RecordAsync(client, token, visit.Id, 10_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("ServiceCharges.VisitNotOpen");
    }

    [Fact]
    public async Task Record_ZeroAmount_Returns400()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);

        using var response = await RecordAsync(client, token, visit.Id, 0m);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Record_UnknownVisit_Returns404()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await RecordAsync(client, token, Guid.CreateVersion7(), 10_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.NotFound");
    }

    [Fact]
    public async Task Record_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/attendance/{Guid.CreateVersion7()}/service-charges",
            new { kind = "Cardio", amount = 10_000m },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Changing the amount ----

    [Fact]
    public async Task ChangeAmount_OpenVisitAndNothingPaid_ReplacesTheAmount()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var charge = await RecordOkAsync(client, token, visit.Id, 10_000m);

        using var response = await ChangeAmountAsync(client, token, charge.Id, 30_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadChargeAsync(response)).Amount.ShouldBe(30_000m);
    }

    /// <summary>
    /// BUSINESS_RULES.md §7: after the first payment it is a financial record, corrected with a
    /// void and a reason rather than edited (§5: financial records are never edited).
    /// </summary>
    [Fact]
    public async Task ChangeAmount_AfterAPayment_Returns422ServiceChargesAlreadyPaid()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var charge = await RecordOkAsync(client, token, visit.Id, 10_000m);
        await PayChargeOkAsync(client, token, charge.Id, 4_000m);

        using var response = await ChangeAmountAsync(client, token, charge.Id, 30_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("ServiceCharges.AlreadyPaid");
        (await StoredChargeAsync(charge.Id)).Amount.ShouldBe(10_000m);
    }

    [Fact]
    public async Task ChangeAmount_AfterCheckOut_Returns422ServiceChargesVisitNotOpen()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var charge = await RecordOkAsync(client, token, visit.Id, 10_000m);
        await CheckOutOkAsync(client, token, visit.Id);

        using var response = await ChangeAmountAsync(client, token, charge.Id, 30_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("ServiceCharges.VisitNotOpen");
    }

    /// <summary>After check-out the screen must stop offering the edit, so the flag says so too.</summary>
    [Fact]
    public async Task CheckOut_WithACharge_ReturnsItWithCanChangeAmountFalse()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        await RecordOkAsync(client, token, visit.Id, 10_000m);

        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/check-out");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var attendance = await ReadAttendanceAsync(response);
        var charge = attendance.ServiceCharges.ShouldHaveSingleItem();
        charge.Amount.ShouldBe(10_000m);
        charge.CanChangeAmount.ShouldBeFalse();
    }

    // ---- Voiding ----

    [Fact]
    public async Task Void_UnpaidCharge_KeepsTheRowAndRecordsTheReason()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var charge = await RecordOkAsync(client, token, visit.Id, 10_000m);

        using var response = await VoidAsync(client, token, charge.Id, "روی عضو اشتباه ثبت شد");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stored = await StoredChargeAsync(charge.Id);
        stored.VoidedAt.ShouldNotBeNull();
        stored.VoidReason.ShouldBe("روی عضو اشتباه ثبت شد");
        stored.VoidedByUserId.ShouldNotBeNull();
        stored.Amount.ShouldBe(10_000m);
    }

    /// <summary>
    /// BUSINESS_RULES.md §5: there is no wallet and no credit balance, so voiding a charge that
    /// has been paid gives the money back in the same transaction — and back by the method it
    /// came in by.
    /// </summary>
    [Fact]
    public async Task Void_PaidCharge_RefundsWhatWasCollectedByTheSameMethod()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var charge = await RecordOkAsync(client, token, visit.Id, 10_000m);
        await PayChargeOkAsync(client, token, charge.Id, 6_000m, method: "Card");
        await PayChargeOkAsync(client, token, charge.Id, 4_000m, method: "Cash");

        using var response = await VoidAsync(client, token, charge.Id, "روی عضو اشتباه ثبت شد");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadChargeAsync(response)).NetPaid.ShouldBe(0m);

        var refunds = (await StoredPaymentsAsync(charge.Id)).Where(p => p.Kind == PaymentKind.Refund).ToList();
        refunds.Count.ShouldBe(2);
        refunds.Single(r => r.Method == PaymentMethod.Card).Amount.ShouldBe(6_000m);
        refunds.Single(r => r.Method == PaymentMethod.Cash).Amount.ShouldBe(4_000m);
        refunds.ShouldAllBe(r => r.Reason == "روی عضو اشتباه ثبت شد");
    }

    [Fact]
    public async Task Void_Twice_Returns422ServiceChargesAlreadyVoided()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var charge = await RecordOkAsync(client, token, visit.Id, 10_000m);
        await VoidOkAsync(client, token, charge.Id, "اشتباه بود");

        using var response = await VoidAsync(client, token, charge.Id, "دوباره");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("ServiceCharges.AlreadyVoided");
    }

    [Fact]
    public async Task Void_BlankReason_Returns400()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var charge = await RecordOkAsync(client, token, visit.Id, 10_000m);

        using var response = await VoidAsync(client, token, charge.Id, "   ");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Staff, not only the Owner (decided with the developer 1405/07/01). §1's permissions table
    /// puts voids with the Owner; this is the documented exception, because the amount is typed at
    /// the desk and the desk has to be able to take back its own mistake.
    /// </summary>
    [Fact]
    public async Task Void_AsStaff_Succeeds()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var charge = await RecordOkAsync(client, token, visit.Id, 10_000m);

        using var response = await VoidAsync(client, token, charge.Id, "اشتباه بود");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---- Cancelling the check-in ----

    /// <summary>
    /// BUSINESS_RULES.md §7: money for a visit that never happened is not owed, so cancelling the
    /// check-in voids the visit's charges — and refunds whatever had been collected for them.
    /// </summary>
    [Fact]
    public async Task CancelCheckIn_WithAPaidCharge_VoidsItAndRefundsTheMoney()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var charge = await RecordOkAsync(client, token, visit.Id, 10_000m);
        await PayChargeOkAsync(client, token, charge.Id, 10_000m);

        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{visit.Id}/cancel");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAttendanceAsync(response)).ServiceCharges.ShouldBeEmpty();

        var stored = await StoredChargeAsync(charge.Id);
        stored.VoidedAt.ShouldNotBeNull();
        stored.VoidReason.ShouldBe("The check-in was cancelled.");

        var payments = await StoredPaymentsAsync(charge.Id);
        payments.Count(p => p.Kind == PaymentKind.Refund).ShouldBe(1);
        payments.Sum(p => p.Kind == PaymentKind.Payment ? p.Amount : -p.Amount).ShouldBe(0m);
    }

    // ---- Paying for it ----

    [Fact]
    public async Task Pay_PartOfTheCharge_LeavesItPartial()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var charge = await RecordOkAsync(client, token, visit.Id, 10_000m);

        using var response = await PayChargeAsync(client, token, charge.Id, 4_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payment = await ReadPaymentAsync(response);
        payment.ServiceChargeId.ShouldBe(charge.Id);
        payment.SubscriptionId.ShouldBeNull();
        payment.TargetNetPaid.ShouldBe(4_000m);
        payment.TargetPaymentStatus.ShouldBe(PaymentStatus.Partial);
    }

    [Fact]
    public async Task Pay_MoreThanTheCharge_Returns422PaymentsOverpayment()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var charge = await RecordOkAsync(client, token, visit.Id, 10_000m);

        using var response = await PayChargeAsync(client, token, charge.Id, 10_001m);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payments.Overpayment");
    }

    [Fact]
    public async Task Pay_VoidedCharge_Returns422ServiceChargesAlreadyVoided()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var charge = await RecordOkAsync(client, token, visit.Id, 10_000m);
        await VoidOkAsync(client, token, charge.Id, "اشتباه بود");

        using var response = await PayChargeAsync(client, token, charge.Id, 10_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("ServiceCharges.AlreadyVoided");
    }

    /// <summary>A charge is paid like anything else (§7), so it joins the member's payment history.</summary>
    [Fact]
    public async Task Pay_ThenListMemberPayments_IncludesTheServiceCharge()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var charge = await RecordOkAsync(client, token, visit.Id, 10_000m);
        await PayChargeOkAsync(client, token, charge.Id, 10_000m);

        var page = await ListPaymentsOkAsync(client, token, visit.MemberId);

        var row = page.Items.ShouldHaveSingleItem();
        row.TargetKind.ShouldBe(PaymentTargetKind.ServiceCharge);
        row.TargetId.ShouldBe(charge.Id);
        row.ServiceKind.ShouldBe(ServiceChargeKind.Cardio);
        row.SubscriptionPlanName.ShouldBeNull();
        row.Amount.ShouldBe(10_000m);
    }

    // ---- The member's debt ----

    [Fact]
    public async Task GetDebt_UnpaidCharge_AppearsBesideTheSubscription()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var charge = await RecordOkAsync(client, token, visit.Id, 10_000m);

        var debt = await GetDebtOkAsync(client, token, visit.MemberId);

        // 900,000 for the plan the member was checked in on, plus the treadmill.
        debt.Total.ShouldBe(910_000m);
        var item = debt.Items.Single(i => i.Kind == PaymentTargetKind.ServiceCharge);
        item.Id.ShouldBe(charge.Id);
        item.ServiceKind.ShouldBe(ServiceChargeKind.Cardio);
        item.PlanName.ShouldBeNull();
        item.EndDate.ShouldBeNull();
        item.Price.ShouldBe(10_000m);
        item.NetPaid.ShouldBe(0m);
        item.Outstanding.ShouldBe(10_000m);
        debt.Total.ShouldBe(debt.Items.Sum(i => i.Outstanding));
    }

    [Fact]
    public async Task GetDebt_PartlyPaidCharge_OwesTheRemainder()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var charge = await RecordOkAsync(client, token, visit.Id, 10_000m);
        await PayChargeOkAsync(client, token, charge.Id, 4_000m);

        var debt = await GetDebtOkAsync(client, token, visit.MemberId);

        debt.Items.Single(i => i.Kind == PaymentTargetKind.ServiceCharge).Outstanding.ShouldBe(6_000m);
    }

    /// <summary>A voided charge owes nothing, the same way a cancelled subscription does (§5).</summary>
    [Fact]
    public async Task GetDebt_VoidedCharge_IsIgnored()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        var charge = await RecordOkAsync(client, token, visit.Id, 10_000m);
        await VoidOkAsync(client, token, charge.Id, "اشتباه بود");

        var debt = await GetDebtOkAsync(client, token, visit.MemberId);

        debt.Items.ShouldNotContain(i => i.Kind == PaymentTargetKind.ServiceCharge);
        debt.Total.ShouldBe(900_000m);
    }

    /// <summary>
    /// The members list carries the same total (task 4.7), so a charge has to reach it too —
    /// it is computed by a different query from the breakdown's.
    /// </summary>
    [Fact]
    public async Task ListMembers_MemberWithACharge_CountsItInTheirDebt()
    {
        var (client, token) = await StaffClientAsync();
        var visit = await CheckedInMemberAsync(client, token);
        await RecordOkAsync(client, token, visit.Id, 10_000m);

        using var response = await SendAsync(client, token, HttpMethod.Get, $"/api/members?Search={Uri.EscapeDataString("رضا")}");
        response.EnsureSuccessStatusCode();
        var page = (await response.Content.ReadFromJsonAsync<MembersPage>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

        page.Items.Single(m => m.Id == visit.MemberId).Debt.ShouldBe(910_000m);
    }

    // ---- Helpers ----

    private sealed record MembersPage(List<MemberRow> Items);

    private sealed record MemberRow(Guid Id, decimal Debt);

    private sealed record PaymentsPage(List<PaymentHistoryResponse> Items);

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    /// <summary>A member with a subscription, checked in: the state every charge starts from.</summary>
    private async Task<AttendanceResponse> CheckedInMemberAsync(HttpClient client, string token)
    {
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();

        using var assigned = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{member.Id}/subscriptions", new { planId = plan.Id });
        assigned.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{member.Id}/attendance/check-in");
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadAttendanceAsync(response);
    }

    private async Task<Member> AddMemberAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("رضا احمدی", $"+98913{suffix:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private async Task<Plan> AddPlanAsync()
    {
        var plan = Plan.Create("پلن", 30, 12, 900_000m).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Plans.Add(plan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return plan;
    }

    private async Task<ServiceCharge> StoredChargeAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ServiceCharges
            .AsNoTracking()
            .SingleAsync(charge => charge.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<List<Payment>> StoredPaymentsAsync(Guid serviceChargeId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments
            .AsNoTracking()
            .Where(payment => payment.ServiceChargeId == serviceChargeId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> RecordAsync(HttpClient client, string token, Guid attendanceId, decimal amount) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/service-charges",
            new { kind = "Cardio", amount });

    private static async Task<ServiceChargeResponse> RecordOkAsync(HttpClient client, string token, Guid attendanceId, decimal amount)
    {
        using var response = await RecordAsync(client, token, attendanceId, amount);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadChargeAsync(response);
    }

    private static Task<HttpResponseMessage> ChangeAmountAsync(HttpClient client, string token, Guid id, decimal amount) =>
        SendAsync(client, token, HttpMethod.Put, $"/api/service-charges/{id}/amount", new { amount });

    private static Task<HttpResponseMessage> VoidAsync(HttpClient client, string token, Guid id, string reason) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/service-charges/{id}/void", new { reason });

    private static async Task VoidOkAsync(HttpClient client, string token, Guid id, string reason)
    {
        using var response = await VoidAsync(client, token, id, reason);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static Task<HttpResponseMessage> PayChargeAsync(
        HttpClient client, string token, Guid id, decimal amount, string method = "Cash") =>
        SendAsync(client, token, HttpMethod.Post, $"/api/service-charges/{id}/payments", new { amount, method });

    private static async Task PayChargeOkAsync(
        HttpClient client, string token, Guid id, decimal amount, string method = "Cash")
    {
        using var response = await PayChargeAsync(client, token, id, amount, method);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task CheckOutOkAsync(HttpClient client, string token, Guid attendanceId)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/check-out");
        response.EnsureSuccessStatusCode();
    }

    private static async Task CancelCheckInOkAsync(HttpClient client, string token, Guid attendanceId)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/cancel");
        response.EnsureSuccessStatusCode();
    }

    private static async Task<MemberDebtResponse> GetDebtOkAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, $"/api/members/{memberId}/debt");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<MemberDebtResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<PaymentsPage> ListPaymentsOkAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, $"/api/members/{memberId}/payments");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PaymentsPage>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<ServiceChargeResponse> ReadChargeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ServiceChargeResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

    private static async Task<AttendanceResponse> ReadAttendanceAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<AttendanceResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

    private static async Task<PaymentResponse> ReadPaymentAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<PaymentResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client, string token, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }
}
