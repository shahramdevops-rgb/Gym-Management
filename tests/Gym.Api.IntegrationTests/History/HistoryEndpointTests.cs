using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances.AutoCheckout;
using Gym.Application.Cafe;
using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.History.ListAttendance;
using Gym.Application.History.ListPayments;
using Gym.Application.History.ListSales;
using Gym.Application.History.ListServiceCharges;
using Gym.Application.Members.GetMemberDebt;
using Gym.Application.ServiceCharges;
using Gym.Application.Subscriptions;
using Gym.Domain.Members;
using Gym.Domain.Payments;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.History;

/// <summary>
/// The gym's history: <c>GET /api/attendance</c>, <c>/api/payments</c>,
/// <c>/api/service-charges</c> (BUSINESS_RULES.md §12 <i>History</i>, roadmap 6.5.25) and the
/// Owner's <c>/api/sales</c> (§12 <i>Sales in the history</i>, roadmap 6.5.30).
/// </summary>
/// <remarks>
/// The API stamps everything with the real clock, so a test that needs a row on another day moves
/// it there with SQL afterwards, and "today" is read from the app's own <see cref="IGymCalendar"/>.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class HistoryEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string StaffName = "سارا میزبان";
    private const string OwnerName = "مدیر باشگاه";

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    private static int _phoneSuffix;

    // ---- Check-ins ----

    [Fact]
    public async Task ListAttendance_NewestFirst_MarksCancelledAndAutoClosedAndNamesWhoCheckedIn()
    {
        var (staff, staffToken) = await StaffClientAsync();
        var (owner, ownerToken) = await OwnerClientAsync();
        var checkedOut = await MemberWithPlanAsync(staff, staffToken, "علی رضایی");
        var cancelled = await MemberWithPlanAsync(staff, staffToken, "مریم کاظمی");
        var leftAtNight = await MemberWithPlanAsync(staff, staffToken, "حسن نوری");

        var first = await TestLockers.CheckInOkAsync(staff, staffToken, checkedOut.Id);
        await PostOkAsync(staff, staffToken, $"/api/attendance/{first.Id}/check-out", body: null);
        var second = await TestLockers.CheckInOkAsync(owner, ownerToken, cancelled.Id);
        await PostOkAsync(staff, staffToken, $"/api/attendance/{second.Id}/cancel", CancelCheckInBody.KeepPurchases);
        var third = await TestLockers.CheckInOkAsync(staff, staffToken, leftAtNight.Id);
        await RunAutoCheckoutAsync();

        var page = await GetOkAsync<PagedResponse<HistoryAttendanceResponse>>(staff, staffToken, "/api/attendance");

        page.TotalCount.ShouldBe(3);
        page.Items.Select(item => item.Id).ShouldBe([third.Id, second.Id, first.Id]);

        var autoClosed = page.Items[0];
        autoClosed.MemberFullName.ShouldBe("حسن نوری");
        autoClosed.AutoClosedAt.ShouldNotBeNull();
        autoClosed.CheckedInByFullName.ShouldBe(StaffName);

        var cancelledRow = page.Items[1];
        cancelledRow.CancelledAt.ShouldNotBeNull();
        cancelledRow.CheckedInByFullName.ShouldBe(OwnerName);

        var ordinary = page.Items[2];
        ordinary.CheckedOutAt.ShouldNotBeNull();
        ordinary.CancelledAt.ShouldBeNull();
        ordinary.AutoClosedAt.ShouldBeNull();
        ordinary.LockerNumber.ShouldBe(first.LockerNumber);
    }

    [Fact]
    public async Task ListAttendance_ByMember_ReturnsOnlyThatMembersVisits()
    {
        var (staff, token) = await StaffClientAsync();
        var member = await MemberWithPlanAsync(staff, token, "علی رضایی");
        var other = await MemberWithPlanAsync(staff, token, "مریم کاظمی");
        var mine = await TestLockers.CheckInOkAsync(staff, token, member.Id);
        await TestLockers.CheckInOkAsync(staff, token, other.Id);

        var page = await GetOkAsync<PagedResponse<HistoryAttendanceResponse>>(
            staff, token, $"/api/attendance?memberId={member.Id}");

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(mine.Id);
    }

    [Fact]
    public async Task ListAttendance_GuestVisit_IsListedUnderTheGuestsNameWithNoMember()
    {
        // BUSINESS_RULES.md §12: guests are listed, marked, with no profile to link to.
        var (staff, token) = await StaffClientAsync();
        var guest = await TestGuests.CheckInOkAsync(staff, token, guestName: "مریم احمدی", lockerNumber: 5);

        var page = await GetOkAsync<PagedResponse<HistoryAttendanceResponse>>(staff, token, "/api/attendance");

        var row = page.Items.ShouldHaveSingleItem();
        row.Id.ShouldBe(guest.Id);
        row.MemberId.ShouldBeNull();
        row.MemberFullName.ShouldBeNull();
        row.GuestName.ShouldBe("مریم احمدی");
        row.LockerNumber.ShouldBe(5);
        row.CheckedInByFullName.ShouldBe(StaffName);
    }

    [Fact]
    public async Task ListAttendance_DateRange_FiltersByTheCheckInDayInTheGymsZone()
    {
        var (staff, token) = await StaffClientAsync();
        var early = await MemberWithPlanAsync(staff, token, "علی رضایی");
        var late = await MemberWithPlanAsync(staff, token, "مریم کاظمی");
        var earlyVisit = await TestLockers.CheckInOkAsync(staff, token, early.Id);
        var lateVisit = await TestLockers.CheckInOkAsync(staff, token, late.Id);
        // 20:40 UTC on 9/19 is 00:10 on 9/20 in Tehran; 20:20 UTC is still 23:50 on 9/19.
        await MoveVisitAsync(earlyVisit.Id, new DateTimeOffset(2026, 9, 19, 20, 20, 0, TimeSpan.Zero));
        await MoveVisitAsync(lateVisit.Id, new DateTimeOffset(2026, 9, 19, 20, 40, 0, TimeSpan.Zero));

        var page = await GetOkAsync<PagedResponse<HistoryAttendanceResponse>>(
            staff, token, "/api/attendance?from=2026-09-20&to=2026-09-20");

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(lateVisit.Id);
    }

    [Fact]
    public async Task ListAttendance_StaffAskingForLastMonth_Returns200()
    {
        // No date limit for check-ins, for either role (§12).
        var (staff, token) = await StaffClientAsync();
        var today = Today();

        using var response = await GetAsync(
            staff, token, $"/api/attendance?from={Iso(today.AddDays(-30))}&to={Iso(today)}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListAttendance_FromAfterTo_Returns400()
    {
        var (staff, token) = await StaffClientAsync();

        using var response = await GetAsync(staff, token, "/api/attendance?from=2026-09-12&to=2026-09-10");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe("General.ValidationFailed");
    }

    // ---- Payments ----

    [Fact]
    public async Task ListPayments_AllThreeSources_NameTheMemberAndWhoTookTheMoney()
    {
        var (staff, token) = await StaffClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        var subscription = await AssignOkAsync(staff, token, member.Id);
        await PayOkAsync(staff, token, $"/api/subscriptions/{subscription.Id}/payments", 900_000m, "Card");
        var visit = await TestLockers.CheckInOkAsync(staff, token, member.Id);
        var cardio = await RecordCardioOkAsync(staff, token, visit.Id, 50_000m);
        await PayOkAsync(staff, token, $"/api/service-charges/{cardio.Id}/payments", 50_000m, "Cash");
        var walkIn = await WalkInOrderAsync(staff, token, 30_000m);

        var page = await PaymentsOkAsync(staff, token, TodayRange());

        page.TotalCount.ShouldBe(3);
        page.Items.ShouldAllBe(item => item.ReceivedByFullName == StaffName);

        var cafe = page.Items[0];
        cafe.Source.ShouldBe(PaymentTargetKind.CafeOrder);
        cafe.TargetId.ShouldBe(walkIn.Id);
        cafe.MemberId.ShouldBeNull();
        cafe.MemberFullName.ShouldBeNull();

        var service = page.Items[1];
        service.Source.ShouldBe(PaymentTargetKind.ServiceCharge);
        service.ServiceKind.ShouldBe(Domain.ServiceCharges.ServiceChargeKind.Cardio);
        service.MemberFullName.ShouldBe("علی رضایی");
        service.Method.ShouldBe(PaymentMethod.Cash);

        var plan = page.Items[2];
        plan.Source.ShouldBe(PaymentTargetKind.Subscription);
        plan.SubscriptionPlan.ShouldBe(new PlanSummary(30, 10, false));
        plan.MemberId.ShouldBe(member.Id);
        plan.Amount.ShouldBe(900_000m);
        plan.Method.ShouldBe(PaymentMethod.Card);

        // Each was taken on its own.
        page.Items.ShouldAllBe(item => item.Settlement == null);
    }

    [Fact]
    public async Task ListPayments_GuestsCafeOrder_CarriesTheGuestsNameAndNoMember()
    {
        var (staff, token) = await StaffClientAsync();
        var guest = await TestGuests.CheckInOkAsync(staff, token, guestName: "مریم احمدی");
        var order = await CafeOrderAsync(staff, token, 30_000m, attendanceId: guest.Id);

        var page = await PaymentsOkAsync(staff, token, TodayRange());

        var row = page.Items.ShouldHaveSingleItem();
        row.TargetId.ShouldBe(order.Id);
        row.MemberId.ShouldBeNull();
        row.GuestName.ShouldBe("مریم احمدی");
    }

    [Fact]
    public async Task ListPayments_RefundsAndUndoneItems_AreListedAndMarked()
    {
        var (staff, staffToken) = await StaffClientAsync();
        var (owner, ownerToken) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        var subscription = await AssignOkAsync(staff, staffToken, member.Id);
        await PayOkAsync(staff, staffToken, $"/api/subscriptions/{subscription.Id}/payments", 900_000m, "Cash");
        await PostOkAsync(owner, ownerToken, $"/api/subscriptions/{subscription.Id}/refunds",
            new { amount = 900_000m, method = "Cash", reason = "اشتباه در ثبت" });
        await PostOkAsync(owner, ownerToken, $"/api/subscriptions/{subscription.Id}/cancel", new { reason = "انصراف" });

        var page = await PaymentsOkAsync(owner, ownerToken, TodayRange());

        page.TotalCount.ShouldBe(2);
        var refund = page.Items[0];
        refund.Kind.ShouldBe(PaymentKind.Refund);
        refund.Reason.ShouldBe("اشتباه در ثبت");
        refund.ReceivedByFullName.ShouldBe(OwnerName);

        var payment = page.Items[1];
        payment.Kind.ShouldBe(PaymentKind.Payment);
        page.Items.ShouldAllBe(item => item.TargetUndone);
    }

    [Fact]
    public async Task ListPayments_VoidedCardio_MarksThePaymentAndListsTheRefundTheVoidWrote()
    {
        var (staff, token) = await StaffClientAsync();
        var member = await MemberWithPlanAsync(staff, token, "علی رضایی");
        var visit = await TestLockers.CheckInOkAsync(staff, token, member.Id);
        var cardio = await RecordCardioOkAsync(staff, token, visit.Id, 50_000m);
        await PayOkAsync(staff, token, $"/api/service-charges/{cardio.Id}/payments", 50_000m, "Card");
        await PostOkAsync(staff, token, $"/api/service-charges/{cardio.Id}/void", new { reason = "مبلغ اشتباه" });

        var page = await PaymentsOkAsync(staff, token, TodayRange() + "&source=ServiceCharge");

        page.Items.Select(item => item.Kind).ShouldBe([PaymentKind.Refund, PaymentKind.Payment]);
        page.Items.ShouldAllBe(item => item.TargetUndone && item.Method == PaymentMethod.Card);
    }

    [Fact]
    public async Task ListPayments_ByMethodSourceAndMember_ReturnOnlyTheMatchingRows()
    {
        var (staff, token) = await StaffClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        var other = await AddMemberAsync("مریم کاظمی");
        var mine = await AssignOkAsync(staff, token, member.Id);
        var theirs = await AssignOkAsync(staff, token, other.Id);
        await PayOkAsync(staff, token, $"/api/subscriptions/{mine.Id}/payments", 100_000m, "Card");
        await PayOkAsync(staff, token, $"/api/subscriptions/{theirs.Id}/payments", 200_000m, "Cash");
        await WalkInOrderAsync(staff, token, 30_000m);

        var byMethod = await PaymentsOkAsync(staff, token, TodayRange() + "&method=Cash");
        var bySource = await PaymentsOkAsync(staff, token, TodayRange() + "&source=CafeOrder");
        var byMember = await PaymentsOkAsync(staff, token, TodayRange() + $"&memberId={member.Id}");

        // The walk-in order was paid in cash too.
        byMethod.Items.Select(item => item.Amount).ShouldBe([30_000m, 200_000m]);
        bySource.Items.ShouldHaveSingleItem().Source.ShouldBe(PaymentTargetKind.CafeOrder);
        byMember.Items.ShouldHaveSingleItem().TargetId.ShouldBe(mine.Id);
    }

    [Fact]
    public async Task ListPayments_SettlementRows_CarryTheWholeHandoverEvenWhenFiltered()
    {
        var (staff, token) = await StaffClientAsync();
        var member = await MemberWithPlanAsync(staff, token, "علی رضایی");
        var visit = await TestLockers.CheckInOkAsync(staff, token, member.Id);
        await RecordCardioOkAsync(staff, token, visit.Id, 50_000m);
        var debt = await GetOkAsync<MemberDebtResponse>(staff, token, $"/api/members/{member.Id}/debt");
        await PostOkAsync(staff, token, $"/api/members/{member.Id}/settlements", new
        {
            amount = debt.Total,
            method = "BankTransfer",
            items = debt.Items.Select(item => new { kind = item.Kind.ToString(), id = item.Id, outstanding = item.Outstanding }),
        });
        await WalkInOrderAsync(staff, token, 30_000m);

        var page = await PaymentsOkAsync(staff, token, TodayRange());
        var cardioOnly = await PaymentsOkAsync(staff, token, TodayRange() + "&source=ServiceCharge");

        page.TotalCount.ShouldBe(3);
        page.Items.Single(item => item.Source == PaymentTargetKind.CafeOrder).Settlement.ShouldBeNull();
        var settled = page.Items.Where(item => item.Source != PaymentTargetKind.CafeOrder).ToList();
        var settlement = settled.Select(item => item.Settlement).Distinct().ShouldHaveSingleItem().ShouldNotBeNull();
        settlement.Total.ShouldBe(debt.Total);
        settlement.ItemCount.ShouldBe(2);

        // The filter leaves one of the two rows; its heading still names the whole handover.
        cardioOnly.Items.ShouldHaveSingleItem().Settlement.ShouldBe(settlement);
    }

    [Fact]
    public async Task ListPayments_DateRange_FiltersByTheDayTheMoneyWasTakenInTheGymsZone()
    {
        var (owner, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        var subscription = await AssignOkAsync(owner, token, member.Id);
        var before = await PayOkAsync(owner, token, $"/api/subscriptions/{subscription.Id}/payments", 100_000m, "Cash");
        var inside = await PayOkAsync(owner, token, $"/api/subscriptions/{subscription.Id}/payments", 200_000m, "Cash");
        // 20:20 UTC on 9/19 is 23:50 on 9/19 in Tehran; 20:40 UTC is 00:10 on 9/20.
        await MovePaymentAsync(before.Id, new DateTimeOffset(2026, 9, 19, 20, 20, 0, TimeSpan.Zero));
        await MovePaymentAsync(inside.Id, new DateTimeOffset(2026, 9, 19, 20, 40, 0, TimeSpan.Zero));

        var page = await PaymentsOkAsync(owner, token, "from=2026-09-20&to=2026-09-21");

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(inside.Id);
    }

    [Fact]
    public async Task ListPayments_StaffAskingForFourDaysAgo_Returns403HistoryTooFarBack()
    {
        var (staff, token) = await StaffClientAsync();
        var today = Today();

        using var response = await GetAsync(
            staff, token, $"/api/payments?from={Iso(today.AddDays(-4))}&to={Iso(today)}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payments.HistoryTooFarBack");
    }

    [Fact]
    public async Task ListPayments_StaffWithNoStartDate_Returns403HistoryTooFarBack()
    {
        var (staff, token) = await StaffClientAsync();

        using var response = await GetAsync(staff, token, "/api/payments");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payments.HistoryTooFarBack");
    }

    [Fact]
    public async Task ListPayments_StaffAskingForThreeDaysAgo_SeesThatDaysPayments()
    {
        var (staff, token) = await StaffClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        var subscription = await AssignOkAsync(staff, token, member.Id);
        var payment = await PayOkAsync(staff, token, $"/api/subscriptions/{subscription.Id}/payments", 100_000m, "Cash");
        var threeDaysAgo = Today().AddDays(-3);
        await MovePaymentAsync(payment.Id, NoonInTehran(threeDaysAgo));

        var page = await PaymentsOkAsync(staff, token, $"from={Iso(threeDaysAgo)}&to={Iso(threeDaysAgo)}");

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(payment.Id);
    }

    [Fact]
    public async Task ListPayments_OwnerAskingForFourDaysAgo_SeesThatDaysPayments()
    {
        var (owner, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        var subscription = await AssignOkAsync(owner, token, member.Id);
        var payment = await PayOkAsync(owner, token, $"/api/subscriptions/{subscription.Id}/payments", 100_000m, "Cash");
        var fourDaysAgo = Today().AddDays(-4);
        await MovePaymentAsync(payment.Id, NoonInTehran(fourDaysAgo));

        var page = await PaymentsOkAsync(owner, token, $"from={Iso(fourDaysAgo)}&to={Iso(fourDaysAgo)}");
        var unbounded = await PaymentsOkAsync(owner, token, query: string.Empty);

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(payment.Id);
        unbounded.Items.ShouldHaveSingleItem().Id.ShouldBe(payment.Id);
    }

    [Fact]
    public async Task ListPayments_UnknownMethod_Returns400()
    {
        var (owner, token) = await OwnerClientAsync();

        using var response = await GetAsync(owner, token, "/api/payments?method=7");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- هوازی ----

    [Fact]
    public async Task ListServiceCharges_VoidedCharge_IsListedAndMarkedWithWhoVoidedIt()
    {
        var (staff, staffToken) = await StaffClientAsync();
        var (owner, ownerToken) = await OwnerClientAsync();
        var member = await MemberWithPlanAsync(staff, staffToken, "علی رضایی");
        var visit = await TestLockers.CheckInOkAsync(staff, staffToken, member.Id);
        var mistaken = await RecordCardioOkAsync(staff, staffToken, visit.Id, 500_000m);
        await PostOkAsync(owner, ownerToken, $"/api/service-charges/{mistaken.Id}/void", new { reason = "صفر اضافه" });
        var corrected = await RecordCardioOkAsync(staff, staffToken, visit.Id, 50_000m);
        await PayOkAsync(staff, staffToken, $"/api/service-charges/{corrected.Id}/payments", 20_000m, "Cash");

        var page = await GetOkAsync<PagedResponse<HistoryServiceChargeResponse>>(
            staff, staffToken, "/api/service-charges");

        page.Items.Select(item => item.Id).ShouldBe([corrected.Id, mistaken.Id]);

        var live = page.Items[0];
        live.MemberFullName.ShouldBe("علی رضایی");
        live.RecordedByFullName.ShouldBe(StaffName);
        live.VoidedAt.ShouldBeNull();
        live.NetPaid.ShouldBe(20_000m);
        live.PaymentStatus.ShouldBe(PaymentStatus.Partial);

        var voided = page.Items[1];
        voided.VoidedAt.ShouldNotBeNull();
        voided.VoidReason.ShouldBe("صفر اضافه");
        voided.VoidedByFullName.ShouldBe(OwnerName);
        voided.RecordedByFullName.ShouldBe(StaffName);
    }

    [Fact]
    public async Task ListServiceCharges_DateRangeAndMember_FilterByTheChargesDayAndMember()
    {
        var (staff, token) = await StaffClientAsync();
        var member = await MemberWithPlanAsync(staff, token, "علی رضایی");
        var other = await MemberWithPlanAsync(staff, token, "مریم کاظمی");
        var mine = await RecordCardioOkAsync(
            staff, token, (await TestLockers.CheckInOkAsync(staff, token, member.Id)).Id, 50_000m);
        var theirs = await RecordCardioOkAsync(
            staff, token, (await TestLockers.CheckInOkAsync(staff, token, other.Id)).Id, 60_000m);
        // A month back is no problem for Staff here: هوازی has no date limit (§12).
        var monthAgo = Today().AddDays(-30);
        await MoveChargeAsync(mine.Id, monthAgo);
        await MoveChargeAsync(theirs.Id, monthAgo.AddDays(-1));

        var byDay = await GetOkAsync<PagedResponse<HistoryServiceChargeResponse>>(
            staff, token, $"/api/service-charges?from={Iso(monthAgo)}&to={Iso(monthAgo)}");
        var byMember = await GetOkAsync<PagedResponse<HistoryServiceChargeResponse>>(
            staff, token, $"/api/service-charges?memberId={other.Id}");

        byDay.Items.ShouldHaveSingleItem().Id.ShouldBe(mine.Id);
        byMember.Items.ShouldHaveSingleItem().Id.ShouldBe(theirs.Id);
    }

    [Fact]
    public async Task ListServiceCharges_FromAfterTo_Returns400()
    {
        var (staff, token) = await StaffClientAsync();

        using var response = await GetAsync(staff, token, "/api/service-charges?from=2026-09-12&to=2026-09-10");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task History_WithoutSigningIn_Returns401()
    {
        var client = Fixture.CreateClient();

        foreach (var path in new[] { "/api/attendance", "/api/payments", "/api/service-charges", "/api/sales" })
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    // ---- Sales ----

    [Fact]
    public async Task ListSales_AllFiveKinds_NewestFirstWithWhatEachSoldAndWhoRecordedIt()
    {
        var (owner, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        var subscription = await AssignOkAsync(owner, token, member.Id);
        var visit = await TestLockers.CheckInOkAsync(owner, token, member.Id);
        var cardio = await RecordChargeOkAsync(owner, token, visit.Id, "Cardio", 50_000m);
        var shop = await RecordShopOkAsync(owner, token, visit.Id, "دستکش", quantity: 2, unitPrice: 300_000m);
        var analysis = await RecordChargeOkAsync(owner, token, visit.Id, "Analysis", 200_000m);
        var productId = await ProductAsync(owner, token, 30_000m);
        var walkIn = await OrderAsync(owner, token, productId, quantity: 2, memberId: null, attendanceId: null, payWith: 60_000m);

        var page = await SalesOkAsync(owner, token, TodayRange());

        page.TotalCount.ShouldBe(5);
        page.Items.Select(item => item.Id).ShouldBe([walkIn.Id, analysis.Id, shop.Id, cardio.Id, subscription.Id]);
        page.Items.Select(item => item.Source).ShouldBe(
            [SaleSource.CafeOrder, SaleSource.Analysis, SaleSource.Miscellaneous, SaleSource.Cardio, SaleSource.Subscription]);

        var cafe = page.Items[0];
        cafe.MemberId.ShouldBeNull();
        cafe.GuestName.ShouldBeNull();
        cafe.CafeItems.ShouldNotBeNull().ShouldHaveSingleItem().ShouldBe(new HistorySaleCafeItem("آب معدنی", 2));
        cafe.Amount.ShouldBe(60_000m);
        cafe.PaymentStatus.ShouldBe(PaymentStatus.Paid);
        cafe.RecordedByFullName.ShouldBe(OwnerName);

        var shopRow = page.Items[2];
        shopRow.Description.ShouldBe("دستکش");
        shopRow.Quantity.ShouldBe(2);
        shopRow.Amount.ShouldBe(600_000m);
        shopRow.MemberFullName.ShouldBe("علی رضایی");
        shopRow.PaymentStatus.ShouldBe(PaymentStatus.Unpaid);

        page.Items[3].RecordedByFullName.ShouldBe(OwnerName);

        // Who sold a plan is not shown (§4).
        var plan = page.Items[4];
        plan.Plan.ShouldBe(new PlanSummary(30, 10, false));
        plan.MemberId.ShouldBe(member.Id);
        plan.Amount.ShouldBe(900_000m);
        plan.RecordedByFullName.ShouldBeNull();
        plan.UndoneAt.ShouldBeNull();
    }

    [Fact]
    public async Task ListSales_BySource_ReturnsOnlyThatKindAndPlansOfBothSorts()
    {
        var (owner, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        var visitor = await AddMemberAsync("مریم کاظمی");
        var membership = await AssignOkAsync(owner, token, member.Id);
        await TestPlans.SetPricesAsync(Fixture, singleVisitPrice: 150_000m);
        var singleVisit = await SellSingleVisitOkAsync(owner, token, visitor.Id);
        var visit = await TestLockers.CheckInOkAsync(owner, token, member.Id);
        var cardio = await RecordChargeOkAsync(owner, token, visit.Id, "Cardio", 50_000m);
        var shop = await RecordShopOkAsync(owner, token, visit.Id, "دستکش", quantity: 1, unitPrice: 300_000m);
        var analysis = await RecordChargeOkAsync(owner, token, visit.Id, "Analysis", 200_000m);
        var productId = await ProductAsync(owner, token, 30_000m);
        var order = await OrderAsync(owner, token, productId, quantity: 1, memberId: member.Id, attendanceId: null, payWith: null);

        var plans = await SalesOkAsync(owner, token, TodayRange() + "&source=Subscription");

        plans.Items.Select(item => item.Id).ShouldBe([singleVisit.Id, membership.Id]);
        plans.Items[0].Plan.ShouldBe(new PlanSummary(1, 1, true));
        plans.Items[1].Plan.ShouldBe(new PlanSummary(30, 10, false));
        (await SalesOkAsync(owner, token, TodayRange() + "&source=Cardio")).Items.ShouldHaveSingleItem().Id.ShouldBe(cardio.Id);
        (await SalesOkAsync(owner, token, TodayRange() + "&source=Miscellaneous")).Items.ShouldHaveSingleItem().Id.ShouldBe(shop.Id);
        (await SalesOkAsync(owner, token, TodayRange() + "&source=Analysis")).Items.ShouldHaveSingleItem().Id.ShouldBe(analysis.Id);
        (await SalesOkAsync(owner, token, TodayRange() + "&source=CafeOrder")).Items.ShouldHaveSingleItem().Id.ShouldBe(order.Id);
    }

    [Fact]
    public async Task ListSales_PaidFilter_SplitsFullyPaidFromStillOwedAndLeavesUndoneOut()
    {
        var (owner, token) = await OwnerClientAsync();
        var paidInFull = await AssignOkAsync(owner, token, (await AddMemberAsync("علی رضایی")).Id);
        await PayOkAsync(owner, token, $"/api/subscriptions/{paidInFull.Id}/payments", 900_000m, "Card");
        var partlyPaid = await AssignOkAsync(owner, token, (await AddMemberAsync("مریم کاظمی")).Id);
        await PayOkAsync(owner, token, $"/api/subscriptions/{partlyPaid.Id}/payments", 100_000m, "Cash");
        var notPaid = await AssignOkAsync(owner, token, (await AddMemberAsync("رضا کریمی")).Id);
        var cancelled = await AssignOkAsync(owner, token, (await AddMemberAsync("حسن نوری")).Id);
        await PostOkAsync(owner, token, $"/api/subscriptions/{cancelled.Id}/cancel", new { reason = "انصراف" });
        var free = await AssignOkAsync(owner, token, (await AddMemberAsync("زهرا امینی")).Id, price: 0m);

        var paid = await SalesOkAsync(owner, token, TodayRange() + "&paid=Paid");
        var unpaid = await SalesOkAsync(owner, token, TodayRange() + "&paid=Unpaid");
        var everything = await SalesOkAsync(owner, token, TodayRange());

        // A free plan owes nothing, so it is paid (§4); a partial payment still owes (§12).
        paid.Items.Select(item => item.Id).ShouldBe([free.Id, paidInFull.Id]);
        paid.Items.ShouldAllBe(item => item.PaymentStatus == PaymentStatus.Paid);
        unpaid.Items.Select(item => item.Id).ShouldBe([notPaid.Id, partlyPaid.Id]);
        unpaid.Items[1].NetPaid.ShouldBe(100_000m);
        unpaid.Items[1].PaymentStatus.ShouldBe(PaymentStatus.Partial);

        everything.TotalCount.ShouldBe(5);
        var cancelledRow = everything.Items.Single(item => item.Id == cancelled.Id);
        cancelledRow.UndoneAt.ShouldNotBeNull();
        cancelledRow.UndoReason.ShouldBe("انصراف");
    }

    [Fact]
    public async Task ListSales_PaidFilter_VoidedChargeIsInNeither()
    {
        var (owner, token) = await OwnerClientAsync();
        var member = await MemberWithPlanAsync(owner, token, "علی رضایی");
        var visit = await TestLockers.CheckInOkAsync(owner, token, member.Id);
        var owed = await RecordChargeOkAsync(owner, token, visit.Id, "Cardio", 50_000m);
        await PostOkAsync(owner, token, $"/api/service-charges/{owed.Id}/void", new { reason = "مبلغ اشتباه" });
        var corrected = await RecordChargeOkAsync(owner, token, visit.Id, "Cardio", 40_000m);

        var unpaid = await SalesOkAsync(owner, token, TodayRange() + "&source=Cardio&paid=Unpaid");
        var paid = await SalesOkAsync(owner, token, TodayRange() + "&source=Cardio&paid=Paid");
        var everything = await SalesOkAsync(owner, token, TodayRange() + "&source=Cardio");

        unpaid.Items.ShouldHaveSingleItem().Id.ShouldBe(corrected.Id);
        paid.TotalCount.ShouldBe(0);
        var voided = everything.Items.Single(item => item.Id == owed.Id);
        voided.UndoneAt.ShouldNotBeNull();
        voided.UndoReason.ShouldBe("مبلغ اشتباه");
    }

    [Fact]
    public async Task ListSales_DateRange_UsesEachKindsOwnDay()
    {
        var (owner, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        var before = await AssignOkAsync(owner, token, member.Id);
        var inside = await AssignOkAsync(owner, token, member.Id);
        var visit = await TestLockers.CheckInOkAsync(owner, token, member.Id);
        var charge = await RecordChargeOkAsync(owner, token, visit.Id, "Analysis", 200_000m);
        var productId = await ProductAsync(owner, token, 30_000m);
        var order = await OrderAsync(owner, token, productId, quantity: 1, memberId: member.Id, attendanceId: null, payWith: null);
        // 20:20 UTC on 9/19 is 23:50 on 9/19 in Tehran; 20:40 UTC is 00:10 on 9/20.
        await MoveSubscriptionSaleAsync(before.Id, new DateTimeOffset(2026, 9, 19, 20, 20, 0, TimeSpan.Zero));
        await MoveSubscriptionSaleAsync(inside.Id, new DateTimeOffset(2026, 9, 19, 20, 40, 0, TimeSpan.Zero));
        await MoveChargeAsync(charge.Id, new DateOnly(2026, 9, 20));
        await MoveOrderAsync(order.Id, new DateOnly(2026, 9, 20));

        var page = await SalesOkAsync(owner, token, "from=2026-09-20&to=2026-09-20");

        page.Items.Select(item => item.Id).ShouldBe([order.Id, charge.Id, inside.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task ListSales_ByMember_LeavesOutOtherMembersAndWalkIns()
    {
        var (owner, token) = await OwnerClientAsync();
        var member = await AddMemberAsync("علی رضایی");
        var mine = await AssignOkAsync(owner, token, member.Id);
        await AssignOkAsync(owner, token, (await AddMemberAsync("مریم کاظمی")).Id);
        var productId = await ProductAsync(owner, token, 30_000m);
        await OrderAsync(owner, token, productId, quantity: 1, memberId: null, attendanceId: null, payWith: 30_000m);

        var page = await SalesOkAsync(owner, token, TodayRange() + $"&memberId={member.Id}");

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(mine.Id);
    }

    [Fact]
    public async Task ListSales_GuestsCafeOrder_CarriesTheGuestsNameAndNoMember()
    {
        var (owner, token) = await OwnerClientAsync();
        var guest = await TestGuests.CheckInOkAsync(owner, token, guestName: "مریم احمدی");
        var productId = await ProductAsync(owner, token, 30_000m);
        var order = await OrderAsync(owner, token, productId, quantity: 1, memberId: null, attendanceId: guest.Id, payWith: null);

        var row = (await SalesOkAsync(owner, token, TodayRange())).Items.ShouldHaveSingleItem();

        row.Id.ShouldBe(order.Id);
        row.MemberId.ShouldBeNull();
        row.GuestName.ShouldBe("مریم احمدی");
        row.PaymentStatus.ShouldBe(PaymentStatus.Unpaid);
    }

    [Fact]
    public async Task ListSales_Staff_Returns403()
    {
        // The sales sections are the Owner's alone (§1, §12 Sales in the history).
        var (staff, token) = await StaffClientAsync();

        using var response = await GetAsync(staff, token, $"/api/sales?{TodayRange()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("paid=Partial")]
    [InlineData("source=Expense")]
    [InlineData("from=2026-09-12&to=2026-09-10")]
    public async Task ListSales_InvalidFilter_Returns400(string query)
    {
        var (owner, token) = await OwnerClientAsync();

        using var response = await GetAsync(owner, token, $"/api/sales?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        var user = await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        await RenameAsync(user.Id, StaffName);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private async Task<(HttpClient Client, string Token)> OwnerClientAsync()
    {
        var user = await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        await RenameAsync(user.Id, OwnerName);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password));
    }

    private DateOnly Today() => Fixture.Services.GetRequiredService<IGymCalendar>().Today();

    private string TodayRange() => $"from={Iso(Today())}&to={Iso(Today())}";

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static DateTimeOffset NoonInTehran(DateOnly date)
    {
        var local = date.ToDateTime(new TimeOnly(12, 0));

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }

    private async Task<Member> AddMemberAsync(string fullName)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed(fullName, $"+98913{suffix:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    /// <summary>A member holding a plan, ready to check in.</summary>
    private async Task<Member> MemberWithPlanAsync(HttpClient client, string token, string fullName)
    {
        var member = await AddMemberAsync(fullName);
        await AssignOkAsync(client, token, member.Id);

        return member;
    }

    private async Task<SubscriptionResponse> AssignOkAsync(
        HttpClient client, string token, Guid memberId, decimal price = 900_000m)
    {
        var plan = await TestPlans.AddAsync(Fixture, price: price);

        using var response = await SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions", plan.Body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<ServiceChargeResponse> RecordCardioOkAsync(
        HttpClient client, string token, Guid attendanceId, decimal amount)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/service-charges", new { kind = "Cardio", amount });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<ServiceChargeResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<ServiceChargeResponse> RecordChargeOkAsync(
        HttpClient client, string token, Guid attendanceId, string kind, decimal amount)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/attendance/{attendanceId}/service-charges", new { kind, amount });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<ServiceChargeResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    /// <summary>One فروشگاه line, left owed: a sale takes no money when it is recorded (§7).</summary>
    private static async Task<ServiceChargeResponse> RecordShopOkAsync(
        HttpClient client, string token, Guid attendanceId, string description, int quantity, decimal unitPrice)
    {
        using var response = await SendAsync(
            client,
            token,
            HttpMethod.Post,
            $"/api/attendance/{attendanceId}/service-charges/shop",
            new { items = new[] { new { description, quantity, unitPrice } } });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<List<ServiceChargeResponse>>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull().ShouldHaveSingleItem();
    }

    private static async Task<SubscriptionResponse> SellSingleVisitOkAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions/single-visit", body: null);
        response.IsSuccessStatusCode.ShouldBeTrue($"Single visit answered {(int)response.StatusCode}.");

        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    /// <summary>A product named «آب معدنی» in a category of its own, for the orders of one test.</summary>
    private static async Task<Guid> ProductAsync(HttpClient client, string token, decimal price)
    {
        using var category = await SendAsync(client, token, HttpMethod.Post, "/api/cafe/categories", new { name = "خوراکی" });
        category.EnsureSuccessStatusCode();
        var categoryId = (await category.Content.ReadFromJsonAsync<ProductCategoryResponse>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull().Id;

        using var product = await SendAsync(
            client, token, HttpMethod.Post, "/api/cafe/products", new { name = "آب معدنی", categoryId, price });
        product.EnsureSuccessStatusCode();

        return (await product.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull().Id;
    }

    /// <summary>A cafe order of one product, paid in cash when <paramref name="payWith"/> is given, otherwise owed.</summary>
    private static async Task<CafeOrderResponse> OrderAsync(
        HttpClient client, string token, Guid productId, int quantity, Guid? memberId, Guid? attendanceId, decimal? payWith)
    {
        using var order = await SendAsync(
            client,
            token,
            HttpMethod.Post,
            "/api/cafe/orders",
            new
            {
                memberId,
                attendanceId,
                items = new[] { new { productId, quantity } },
                payment = payWith is { } amount ? new { amount, method = "Cash", referenceNumber = (string?)null } : null,
            });
        order.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await order.Content.ReadFromJsonAsync<CafeOrderResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<PagedResponse<HistorySaleResponse>> SalesOkAsync(
        HttpClient client, string token, string query) =>
        await GetOkAsync<PagedResponse<HistorySaleResponse>>(client, token, $"/api/sales?{query}");

    private static async Task<Application.Payments.PaymentResponse> PayOkAsync(
        HttpClient client, string token, string path, decimal amount, string method)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, new { amount, method });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<Application.Payments.PaymentResponse>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    /// <summary>A walk-in's cafe order, paid in full in cash at the till.</summary>
    private static Task<CafeOrderResponse> WalkInOrderAsync(HttpClient client, string token, decimal price) =>
        CafeOrderAsync(client, token, price, attendanceId: null);

    /// <summary>A cafe order of one product, paid in full in cash: a walk-in's, or on a guest's visit.</summary>
    private static async Task<CafeOrderResponse> CafeOrderAsync(
        HttpClient client, string token, decimal price, Guid? attendanceId)
    {
        using var category = await SendAsync(client, token, HttpMethod.Post, "/api/cafe/categories", new { name = "نوشیدنی" });
        category.EnsureSuccessStatusCode();
        var categoryId = (await category.Content.ReadFromJsonAsync<ProductCategoryResponse>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull().Id;

        using var product = await SendAsync(
            client, token, HttpMethod.Post, "/api/cafe/products", new { name = "آب معدنی", categoryId, price });
        product.EnsureSuccessStatusCode();
        var productId = (await product.Content.ReadFromJsonAsync<ProductResponse>(
            TestContext.Current.CancellationToken)).ShouldNotBeNull().Id;

        using var order = await SendAsync(
            client,
            token,
            HttpMethod.Post,
            "/api/cafe/orders",
            new
            {
                memberId = (Guid?)null,
                attendanceId,
                items = new[] { new { productId, quantity = 1 } },
                payment = new { amount = price, method = "Cash", referenceNumber = (string?)null },
            });
        order.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await order.Content.ReadFromJsonAsync<CafeOrderResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<PagedResponse<HistoryPaymentResponse>> PaymentsOkAsync(
        HttpClient client, string token, string query) =>
        await GetOkAsync<PagedResponse<HistoryPaymentResponse>>(
            client, token, query.Length == 0 ? "/api/payments" : $"/api/payments?{query}");

    private static async Task PostOkAsync(HttpClient client, string token, string path, object? body)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path, body);
        response.IsSuccessStatusCode.ShouldBeTrue($"POST {path} answered {(int)response.StatusCode}.");
    }

    private static async Task<T> GetOkAsync<T>(HttpClient client, string token, string path)
        where T : class
    {
        using var response = await GetAsync(client, token, path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string token, string path) =>
        SendAsync(client, token, HttpMethod.Get, path, body: null);

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client, string token, HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private async Task RunAutoCheckoutAsync()
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AutoCheckoutHandler>().Handle(TestContext.Current.CancellationToken);
    }

    /// <summary>Gives a user the full name the assertions look for; <see cref="TestUsers"/> names everyone alike.</summary>
    private async Task RenameAsync(Guid userId, string fullName)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE users SET full_name = {fullName} WHERE id = {userId}",
            TestContext.Current.CancellationToken);
    }

    /// <summary>Moves a visit to another moment, closing it an hour later so it is not left open.</summary>
    private async Task MoveVisitAsync(Guid attendanceId, DateTimeOffset checkedInAt)
    {
        var checkedOutAt = checkedInAt.AddHours(1);

        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE attendances SET checked_in_at = {checkedInAt}, checked_out_at = {checkedOutAt} WHERE id = {attendanceId}",
            TestContext.Current.CancellationToken);
    }

    private async Task MovePaymentAsync(Guid paymentId, DateTimeOffset paidAt)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE payments SET paid_at = {paidAt} WHERE id = {paymentId}",
            TestContext.Current.CancellationToken);
    }

    private async Task MoveChargeAsync(Guid chargeId, DateOnly chargedOn)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE service_charges SET charged_on = {chargedOn} WHERE id = {chargeId}",
            TestContext.Current.CancellationToken);
    }

    private async Task MoveSubscriptionSaleAsync(Guid subscriptionId, DateTimeOffset createdAt)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE subscriptions SET created_at = {createdAt} WHERE id = {subscriptionId}",
            TestContext.Current.CancellationToken);
    }

    private async Task MoveOrderAsync(Guid orderId, DateOnly orderedOn)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE cafe_orders SET ordered_on = {orderedOn} WHERE id = {orderId}",
            TestContext.Current.CancellationToken);
    }
}
