using FluentValidation;

using Gym.Application.Accounts.RenameUser;
using Gym.Application.Accounts.SetPassword;
using Gym.Application.Accounts.UnlockUser;
using Gym.Application.Attendances.AutoCheckout;
using Gym.Application.Attendances.CardioOnlyCheckIn;
using Gym.Application.Attendances.CancelCheckIn;
using Gym.Application.Attendances.CheckIn;
using Gym.Application.Attendances.CheckOut;
using Gym.Application.Attendances.GuestCheckIn;
using Gym.Application.Attendances.ListCurrentlyInside;
using Gym.Application.Attendances.ListMemberAttendance;
using Gym.Application.Attendances.MoveLocker;
using Gym.Application.Attendances.SettleGuestVisit;
using Gym.Application.Attendances.TodayByHour;
using Gym.Application.Auth.ChangePassword;
using Gym.Application.Auth.GetCurrentUser;
using Gym.Application.Auth.Login;
using Gym.Application.Auth.Logout;
using Gym.Application.Auth.Refresh;
using Gym.Application.Cafe.CancelCafeOrder;
using Gym.Application.Cafe.CreateCafeOrder;
using Gym.Application.Cafe.CreateProduct;
using Gym.Application.Cafe.CreateProductCategory;
using Gym.Application.Cafe.DeleteProductCategory;
using Gym.Application.Cafe.GetCafeOrder;
using Gym.Application.Cafe.GetProduct;
using Gym.Application.Cafe.ListCafeOrders;
using Gym.Application.Cafe.ListMemberCafeOrders;
using Gym.Application.Cafe.ListProductCategories;
using Gym.Application.Cafe.ListProducts;
using Gym.Application.Cafe.SetProductActive;
using Gym.Application.Cafe.SetProductCategoryActive;
using Gym.Application.Cafe.UpdateProduct;
using Gym.Application.Cafe.UpdateProductCategory;
using Gym.Application.Expenses.CreateExpenseCategory;
using Gym.Application.Expenses.GetExpense;
using Gym.Application.Expenses.ListExpenseCategories;
using Gym.Application.Expenses.ListExpenses;
using Gym.Application.Expenses.RecordExpense;
using Gym.Application.Expenses.UpdateExpense;
using Gym.Application.Expenses.UpdateExpenseCategory;
using Gym.Application.Expenses.VoidExpense;
using Gym.Application.History.ListAttendance;
using Gym.Application.History.ListPayments;
using Gym.Application.History.ListSales;
using Gym.Application.History.ListServiceCharges;
using Gym.Application.History.PaymentTotals;
using Gym.Application.History.SalesTotals;
using Gym.Application.Lockers.GetLocker;
using Gym.Application.Lockers.ListLockers;
using Gym.Application.Lockers.ListLockerVisitsToday;
using Gym.Application.Lockers.LockerUsage;
using Gym.Application.Lockers.SetLockerOutOfService;
using Gym.Application.Members.CreateMember;
using Gym.Application.Members.GetMember;
using Gym.Application.Members.GetMemberDebt;
using Gym.Application.Members.ListMembers;
using Gym.Application.Members.SetMemberActive;
using Gym.Application.Members.UpdateMember;
using Gym.Application.Payments.ListMemberPayments;
using Gym.Application.Payments.RegisterPayment;
using Gym.Application.Payments.RegisterRefund;
using Gym.Application.Payments.SettleMemberDebt;
using Gym.Application.Pricing.GetPrices;
using Gym.Application.Pricing.UpdatePrices;
using Gym.Application.ServiceCharges.ChangeServiceChargeAmount;
using Gym.Application.ServiceCharges.RecordShopSale;
using Gym.Application.ServiceCharges.RecordServiceCharge;
using Gym.Application.ServiceCharges.VoidServiceCharge;
using Gym.Application.Staff.CreateStaff;
using Gym.Application.Staff.GetStaff;
using Gym.Application.Staff.ListStaff;
using Gym.Application.Staff.ResetStaffPassword;
using Gym.Application.Staff.SetStaffActive;
using Gym.Application.Staff.UnlockStaff;
using Gym.Application.Subscriptions;
using Gym.Application.Subscriptions.AssignSubscription;
using Gym.Application.Subscriptions.CancelSubscription;
using Gym.Application.Subscriptions.FreezeSubscription;
using Gym.Application.Subscriptions.GetSubscription;
using Gym.Application.Subscriptions.ListMemberSubscriptions;
using Gym.Application.Subscriptions.RenewSubscription;
using Gym.Application.Subscriptions.SellSingleVisit;
using Gym.Application.Subscriptions.UnfreezeSubscription;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gym.Application;

/// <summary>
/// The one entry point Gym.Api uses to wire up the use-case layer, so <c>Program.cs</c> names
/// layers rather than individual services.
/// </summary>
/// <remarks>
/// Deliberately thin today: handlers are plain injected classes and the first of them arrives
/// in Phase 2. The seam exists now so that adding a use case later means editing this layer,
/// never the composition root.
/// </remarks>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Use cases need a clock, because CLAUDE.md forbids DateTime.UtcNow: business dates
        // such as "is this subscription expired today" must be steerable from a test.
        // AddInfrastructure registers the same singleton for the audit interceptor, so both
        // use TryAdd: each layer declares the dependency it actually has, and whichever runs
        // first wins without the other throwing or silently replacing it.
        services.TryAddSingleton(TimeProvider.System);

        // One scan instead of a registration line per validator: a new
        // Application/<Feature>/<UseCase>/Validator.cs is picked up by simply existing.
        // Forgetting a registration line would not fail the build — it would silently disable
        // validation for that endpoint, which is the worst possible way to find out. Each
        // validator is registered as IValidator<TCommand>, which is what ValidationFilter<T>
        // in Gym.Api resolves.
        services.AddValidatorsFromAssembly(Common.AssemblyReference.Assembly, includeInternalTypes: true);

        // Handlers are plain classes resolved by type, one line each (CLAUDE.md: no MediatR).
        // Scoped, because they depend on scoped services such as IAppDbContext.
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<ChangePasswordHandler>();
        services.AddScoped<GetCurrentUserHandler>();

        services.AddScoped<CreateMemberHandler>();
        services.AddScoped<UpdateMemberHandler>();
        services.AddScoped<GetMemberHandler>();
        services.AddScoped<GetMemberDebtHandler>();
        services.AddScoped<ListMembersHandler>();
        services.AddScoped<SetMemberActiveHandler>();

        services.AddScoped<GetPricesHandler>();
        services.AddScoped<UpdatePricesHandler>();

        services.AddScoped<GetLockerHandler>();
        services.AddScoped<ListLockersHandler>();
        services.AddScoped<ListLockerVisitsTodayHandler>();
        services.AddScoped<LockerUsageHandler>();
        services.AddScoped<SetLockerOutOfServiceHandler>();

        services.AddScoped<CheckInHandler>();
        services.AddScoped<CheckOutHandler>();
        services.AddScoped<CancelCheckInHandler>();
        services.AddScoped<GuestCheckInHandler>();
        services.AddScoped<CardioOnlyCheckInHandler>();
        services.AddScoped<SettleGuestVisitHandler>();
        services.AddScoped<MoveLockerHandler>();
        services.AddScoped<ListCurrentlyInsideHandler>();
        services.AddScoped<ListMemberAttendanceHandler>();
        services.AddScoped<TodayByHourHandler>();
        services.AddScoped<AutoCheckoutHandler>();

        services.AddScoped<SubscriptionSeller>();
        services.AddScoped<AssignSubscriptionHandler>();
        services.AddScoped<RenewSubscriptionHandler>();
        services.AddScoped<SellSingleVisitHandler>();
        services.AddScoped<GetSubscriptionHandler>();
        services.AddScoped<ListMemberSubscriptionsHandler>();
        services.AddScoped<FreezeSubscriptionHandler>();
        services.AddScoped<UnfreezeSubscriptionHandler>();
        services.AddScoped<CancelSubscriptionHandler>();

        services.AddScoped<RegisterPaymentHandler>();
        services.AddScoped<RegisterRefundHandler>();
        services.AddScoped<RegisterServiceChargePaymentHandler>();
        services.AddScoped<RegisterCafeOrderPaymentHandler>();
        services.AddScoped<ListMemberPaymentsHandler>();
        services.AddScoped<SettleMemberDebtHandler>();

        services.AddScoped<RecordServiceChargeHandler>();
        services.AddScoped<RecordShopSaleHandler>();
        services.AddScoped<ChangeServiceChargeAmountHandler>();
        services.AddScoped<VoidServiceChargeHandler>();

        services.AddScoped<ListAttendanceHandler>();
        services.AddScoped<ListPaymentsHandler>();
        services.AddScoped<ListServiceChargesHandler>();
        services.AddScoped<ListSalesHandler>();
        services.AddScoped<PaymentRows>();
        services.AddScoped<PaymentTotalsHandler>();
        services.AddScoped<SaleRows>();
        services.AddScoped<SalesTotalsHandler>();

        services.AddScoped<CreateCafeOrderHandler>();
        services.AddScoped<GetCafeOrderHandler>();
        services.AddScoped<CancelCafeOrderHandler>();
        services.AddScoped<ListCafeOrdersHandler>();
        services.AddScoped<ListMemberCafeOrdersHandler>();

        services.AddScoped<CreateProductCategoryHandler>();
        services.AddScoped<UpdateProductCategoryHandler>();
        services.AddScoped<DeleteProductCategoryHandler>();
        services.AddScoped<ListProductCategoriesHandler>();
        services.AddScoped<SetProductCategoryActiveHandler>();

        services.AddScoped<CreateProductHandler>();
        services.AddScoped<UpdateProductHandler>();
        services.AddScoped<GetProductHandler>();
        services.AddScoped<ListProductsHandler>();
        services.AddScoped<SetProductActiveHandler>();

        services.AddScoped<CreateExpenseCategoryHandler>();
        services.AddScoped<UpdateExpenseCategoryHandler>();
        services.AddScoped<ListExpenseCategoriesHandler>();

        services.AddScoped<RecordExpenseHandler>();
        services.AddScoped<UpdateExpenseHandler>();
        services.AddScoped<VoidExpenseHandler>();
        services.AddScoped<GetExpenseHandler>();
        services.AddScoped<ListExpensesHandler>();

        services.AddScoped<CreateStaffHandler>();
        services.AddScoped<ListStaffHandler>();
        services.AddScoped<GetStaffHandler>();
        services.AddScoped<SetStaffActiveHandler>();
        services.AddScoped<ResetStaffPasswordHandler>();
        services.AddScoped<UnlockStaffHandler>();

        // The server console (./server.sh unlock | set-password | rename).
        services.AddScoped<UnlockUserHandler>();
        services.AddScoped<SetPasswordHandler>();
        services.AddScoped<RenameUserHandler>();

        return services;
    }
}
