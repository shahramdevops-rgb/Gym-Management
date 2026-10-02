using Gym.Domain.Cafe;

namespace Gym.Domain.Tests.Cafe;

public sealed class CafeOrderTests
{
    private static readonly Guid CategoryId = Guid.CreateVersion7();

    private static readonly DateOnly Today = new(2026, 9, 26);

    private static readonly Guid StaffId = Guid.CreateVersion7();

    [Fact]
    public void Create_OneLine_TotalsTheLine()
    {
        var water = Product("آب معدنی", 15_000m);

        var order = CafeOrder.Create(memberId: null, [(water, 2)], Today, StaffId).Value;

        order.TotalAmount.ShouldBe(30_000m);
        order.OrderedOn.ShouldBe(Today);
        order.PlacedByUserId.ShouldBe(StaffId);
        order.IsCancelled.ShouldBeFalse();
    }

    [Fact]
    public void Create_DuringAVisit_RemembersTheVisit()
    {
        var memberId = Guid.CreateVersion7();
        var visitId = Guid.CreateVersion7();

        var order = CafeOrder.Create(memberId, [(Product("آب معدنی", 15_000m), 1)], Today, StaffId, visitId).Value;

        order.AttendanceId.ShouldBe(visitId);
        order.MemberId.ShouldBe(memberId);
    }

    [Fact]
    public void Create_VisitWithoutAMember_IsRefused()
    {
        // A member's visit names its member; an order with none goes through CreateForGuestVisit.
        var result = CafeOrder.Create(
            memberId: null, [(Product("آب معدنی", 15_000m), 1)], Today, StaffId, Guid.CreateVersion7());

        result.Error.ShouldBe(CafeOrderErrors.VisitOfAnotherMember);
    }

    [Fact]
    public void CreateForGuestVisit_OneLine_NamesTheVisitAndNoMember()
    {
        var visitId = Guid.CreateVersion7();

        var order = CafeOrder.CreateForGuestVisit(visitId, [(Product("آب معدنی", 15_000m), 2)], Today, StaffId).Value;

        order.AttendanceId.ShouldBe(visitId);
        order.MemberId.ShouldBeNull();
        order.TotalAmount.ShouldBe(30_000m);
        order.Outstanding(netPaid: 0m).ShouldBe(30_000m);
    }

    [Fact]
    public void CreateForGuestVisit_NoLines_FailsWithNoItems()
    {
        CafeOrder.CreateForGuestVisit(Guid.CreateVersion7(), [], Today, StaffId)
            .Error.ShouldBe(CafeOrderErrors.NoItems);
    }

    [Fact]
    public void Create_SeveralLines_TotalsThemAll()
    {
        var order = CafeOrder.Create(
            memberId: null,
            [(Product("آب معدنی", 15_000m), 2), (Product("کیک", 25_000m), 1)],
            Today,
            StaffId).Value;

        order.TotalAmount.ShouldBe(55_000m);
        order.Items.Count.ShouldBe(2);
    }

    [Fact]
    public void Create_Line_SnapshotsTheProductsNameAndPrice()
    {
        // The snapshot is what makes editing a price safe: yesterday's order keeps yesterday's
        // figure (BUSINESS_RULES.md §8).
        var water = Product("آب معدنی", 15_000m);
        var order = CafeOrder.Create(memberId: null, [(water, 3)], Today, StaffId).Value;

        water.Update("آب معدنی بزرگ", CategoryId, 20_000m);

        var line = order.Items.ShouldHaveSingleItem();
        line.ProductId.ShouldBe(water.Id);
        line.ProductName.ShouldBe("آب معدنی");
        line.UnitPrice.ShouldBe(15_000m);
        line.Quantity.ShouldBe(3);
        line.LineTotal.ShouldBe(45_000m);
        order.TotalAmount.ShouldBe(45_000m);
    }

    [Fact]
    public void Create_WithAMember_PutsItOnThatAccount()
    {
        var memberId = Guid.CreateVersion7();

        var order = CafeOrder.Create(memberId, [(Product("چای", 10_000m), 1)], Today, StaffId).Value;

        order.MemberId.ShouldBe(memberId);
    }

    [Fact]
    public void Create_NoLines_FailsWithNoItems()
    {
        CafeOrder.Create(memberId: null, [], Today, StaffId).Error.ShouldBe(CafeOrderErrors.NoItems);
    }

    [Fact]
    public void Create_MoreLinesThanTheLimit_FailsWithTooManyItems()
    {
        var lines = Enumerable.Range(0, CafeOrder.MaxItems + 1)
            .Select(index => (Product($"محصول {index}", 1_000m), 1))
            .ToList();

        CafeOrder.Create(memberId: null, lines, Today, StaffId).Error.ShouldBe(CafeOrderErrors.TooManyItems);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(CafeOrderItem.MaxQuantity + 1)]
    public void Create_QuantityOutOfRange_FailsWithQuantityInvalid(int quantity)
    {
        CafeOrder.Create(memberId: null, [(Product("چای", 10_000m), quantity)], Today, StaffId)
            .Error.ShouldBe(CafeOrderErrors.QuantityInvalid);
    }

    [Fact]
    public void Create_SameProductTwice_FailsWithDuplicateProduct()
    {
        // Two lines of one product would total correctly and read as two purchases of one thing.
        var water = Product("آب معدنی", 15_000m);

        CafeOrder.Create(memberId: null, [(water, 1), (water, 2)], Today, StaffId)
            .Error.ShouldBe(CafeOrderErrors.DuplicateProduct);
    }

    [Fact]
    public void Create_FreeProduct_IsAllowedAndTotalsZero()
    {
        var order = CafeOrder.Create(memberId: null, [(Product("لیوان آب", 0m), 1)], Today, StaffId).Value;

        order.TotalAmount.ShouldBe(0m);
    }

    // ---- Outstanding ----

    [Fact]
    public void Outstanding_NothingPaid_IsTheWholeTotal()
    {
        var order = CafeOrder.Create(memberId: null, [(Product("کیک", 25_000m), 2)], Today, StaffId).Value;

        order.Outstanding(netPaid: 0m).ShouldBe(50_000m);
    }

    [Fact]
    public void Outstanding_PartlyPaid_IsTheRest()
    {
        var order = CafeOrder.Create(memberId: null, [(Product("کیک", 25_000m), 2)], Today, StaffId).Value;

        order.Outstanding(netPaid: 20_000m).ShouldBe(30_000m);
    }

    [Fact]
    public void Outstanding_OverPaid_IsNeverNegative()
    {
        var order = CafeOrder.Create(memberId: null, [(Product("کیک", 25_000m), 1)], Today, StaffId).Value;

        order.Outstanding(netPaid: 30_000m).ShouldBe(0m);
    }

    [Fact]
    public void Outstanding_CancelledOrder_IsZeroHoweverLittleWasPaid()
    {
        // A cancelled order owes nothing (BUSINESS_RULES.md §5 Member debt).
        var order = CafeOrder.Create(memberId: null, [(Product("کیک", 25_000m), 1)], Today, StaffId).Value;
        order.Cancel("اشتباه ثبت شد", Now, StaffId);

        order.Outstanding(netPaid: 0m).ShouldBe(0m);
    }

    // ---- Cancel ----

    [Fact]
    public void Cancel_WithAReason_RecordsWhoWhenAndWhy()
    {
        var order = CafeOrder.Create(memberId: null, [(Product("کیک", 25_000m), 1)], Today, StaffId).Value;

        var result = order.Cancel("  مشتری پس داد ", Now, StaffId);

        result.IsSuccess.ShouldBeTrue();
        order.IsCancelled.ShouldBeTrue();
        order.CancelledAt.ShouldBe(Now);
        order.CancelReason.ShouldBe("مشتری پس داد");
        order.CancelledByUserId.ShouldBe(StaffId);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_FailsAndKeepsTheFirstReason()
    {
        var order = CafeOrder.Create(memberId: null, [(Product("کیک", 25_000m), 1)], Today, StaffId).Value;
        order.Cancel("دلیل اول", Now, StaffId);

        order.Cancel("دلیل دوم", Now, StaffId).Error.ShouldBe(CafeOrderErrors.AlreadyCancelled);

        order.CancelReason.ShouldBe("دلیل اول");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_BlankReason_FailsAndChangesNothing(string reason)
    {
        var order = CafeOrder.Create(memberId: null, [(Product("کیک", 25_000m), 1)], Today, StaffId).Value;

        order.Cancel(reason, Now, StaffId).Error.ShouldBe(CafeOrderErrors.CancelReasonRequired);

        order.IsCancelled.ShouldBeFalse();
    }

    [Fact]
    public void Cancel_ReasonOverTheLimit_FailsWithCancelReasonTooLong()
    {
        var order = CafeOrder.Create(memberId: null, [(Product("کیک", 25_000m), 1)], Today, StaffId).Value;

        order.Cancel(new string('ا', CafeOrder.CancelReasonMaxLength + 1), Now, StaffId)
            .Error.ShouldBe(CafeOrderErrors.CancelReasonTooLong);
    }

    private static DateTimeOffset Now => new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private static Product Product(string name, decimal price) =>
        Domain.Cafe.Product.Create(name, CategoryId, price).Value;
}
