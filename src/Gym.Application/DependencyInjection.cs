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
using Gym.Application.Audit.ListAuditLogs;
using Gym.Application.Audit.ListAuditUsers;
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
using Gym.Application.Coaching.AcceptTrainerRequest;
using Gym.Application.Coaching.CancelTrainerRequest;
using Gym.Application.Coaching.DeclineTrainerRequest;
using Gym.Application.Coaching.EndMyTrainer;
using Gym.Application.Coaching.EndStudent;
using Gym.Application.Coaching.GetMyTrainer;
using Gym.Application.Coaching.GetStudents;
using Gym.Application.Coaching.ListTrainerChoices;
using Gym.Application.Coaching.RequestTrainer;
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
using Gym.Application.Reports.GetAbsenceSmsEffect;
using Gym.Application.Reports.GetAttendanceReport;
using Gym.Application.Reports.GetFinancialReport;
using Gym.Application.Reports.GetMembersReport;
using Gym.Application.Reports.GetNeedsAttention;
using Gym.Application.Notifications.GetSignInCodeSms;
using Gym.Application.Reports.GetReceivables;
using Gym.Application.Reports.GetSubscriptionsSnapshot;
using Gym.Application.Reports.GetTodaySummary;
using Gym.Application.Reports.GetTopCafeProducts;
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
using Gym.Application.Notifications;
using Gym.Application.Notifications.Broadcasts;
using Gym.Application.Notifications.Broadcasts.GetBroadcastAudience;
using Gym.Application.Notifications.Broadcasts.ListBroadcasts;
using Gym.Application.Notifications.Broadcasts.PreviewBroadcast;
using Gym.Application.Notifications.Broadcasts.RecoverBroadcasts;
using Gym.Application.Notifications.Broadcasts.ResumeBroadcast;
using Gym.Application.Notifications.Broadcasts.RunBroadcast;
using Gym.Application.Notifications.Broadcasts.SendBroadcast;
using Gym.Application.Notifications.CheckSmsDelivery;
using Gym.Application.Notifications.GetSmsCredit;
using Gym.Application.Notifications.GetSmsSamples;
using Gym.Application.Notifications.GetSmsSettings;
using Gym.Application.Notifications.ListSmsMessages;
using Gym.Application.Notifications.ResendCancelledSms;
using Gym.Application.Notifications.ResendSms;
using Gym.Application.Notifications.SendSubscriptionReceipt;
using Gym.Application.Notifications.SendWelcome;
using Gym.Application.Notifications.SendTrainerRequestSms;
using Gym.Application.Notifications.SendWelcomeBack;
using Gym.Application.Notifications.SendDailySms;
using Gym.Application.Notifications.UpdateSmsSettings;
using Gym.Application.Payables.CancelPayable;
using Gym.Application.Payables.GetPayable;
using Gym.Application.Payables.ListPayables;
using Gym.Application.Payables.ListPayablesDueSoon;
using Gym.Application.Payables.MarkPayablePaid;
using Gym.Application.Payables.RegisterPayable;
using Gym.Application.Payables.RevertPayable;
using Gym.Application.Payables.UpdatePayable;
using Gym.Application.Payments.ListMemberPayments;
using Gym.Application.Payments.RegisterPayment;
using Gym.Application.Payments.SettleMemberDebt;
using Gym.Application.Pricing.GetPrices;
using Gym.Application.Pricing.UpdatePrices;
using Gym.Application.ServiceCharges.ChangeServiceChargeAmount;
using Gym.Application.ServiceCharges.RecordShopSale;
using Gym.Application.ServiceCharges.RecordServiceCharge;
using Gym.Application.ServiceCharges.VoidServiceCharge;
using Gym.Application.SiteAuth;
using Gym.Application.SiteAuth.GetMe;
using Gym.Application.SiteAuth.Logout;
using Gym.Application.SiteAuth.Refresh;
using Gym.Application.SiteAuth.RequestCode;
using Gym.Application.SiteAuth.SignIn;
using Gym.Application.SiteAuth.SignUp;
using Gym.Application.SiteAuth.VerifyCode;
using Gym.Application.SiteRecords.GetMyDebt;
using Gym.Application.SiteRecords.GetMyDetails;
using Gym.Application.SiteRecords.GetMyPlan;
using Gym.Application.SiteRecords.ListMyCafeOrders;
using Gym.Application.SiteRecords.ListMyPayments;
using Gym.Application.SiteRecords.ListMyPlans;
using Gym.Application.SiteRecords.ListMyVisits;
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
using Gym.Application.Trainers.CreateTrainer;
using Gym.Application.Trainers.ListTrainers;
using Gym.Application.Trainers.RestartTrainer;
using Gym.Application.Trainers.StopTrainer;
using Gym.Application.Trainers.UpdateTrainer;

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

        // The site's sign-in (BUSINESS_RULES.md §15, task 15.1).
        services.AddScoped<RequestCodeHandler>();
        services.AddScoped<VerifyCodeHandler>();
        services.AddScoped<SiteAccountLinks>();
        services.AddScoped<SiteSessions>();
        services.AddScoped<SignInHandler>();
        services.AddScoped<SignUpHandler>();
        services.AddScoped<SiteRefreshHandler>();
        services.AddScoped<SiteLogoutHandler>();
        services.AddScoped<GetSiteMeHandler>();

        // Trainers in the panel (BUSINESS_RULES.md §15, task 15.2).
        services.AddScoped<ListTrainersHandler>();
        services.AddScoped<CreateTrainerHandler>();
        services.AddScoped<UpdateTrainerHandler>();
        services.AddScoped<StopTrainerHandler>();
        services.AddScoped<RestartTrainerHandler>();

        // A member and their trainer on the site (BUSINESS_RULES.md §15, task 15.4).
        services.AddScoped<ListTrainerChoicesHandler>();
        services.AddScoped<GetMyTrainerHandler>();
        services.AddScoped<RequestTrainerHandler>();
        services.AddScoped<CancelTrainerRequestHandler>();
        services.AddScoped<EndMyTrainerHandler>();
        services.AddScoped<GetStudentsHandler>();
        services.AddScoped<AcceptTrainerRequestHandler>();
        services.AddScoped<DeclineTrainerRequestHandler>();
        services.AddScoped<EndStudentHandler>();

        // The member's own records on the site (BUSINESS_RULES.md §15, task 15.5).
        services.AddScoped<GetMyDetailsHandler>();
        services.AddScoped<GetMyPlanHandler>();
        services.AddScoped<GetMyDebtHandler>();
        services.AddScoped<ListMyVisitsHandler>();
        services.AddScoped<ListMyPlansHandler>();
        services.AddScoped<ListMyPaymentsHandler>();
        services.AddScoped<ListMyCafeOrdersHandler>();

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

        services.AddScoped<GetFinancialReportHandler>();
        services.AddScoped<GetReceivablesHandler>();
        services.AddScoped<GetAttendanceReportHandler>();
        services.AddScoped<GetSubscriptionsSnapshotHandler>();
        services.AddScoped<GetTopCafeProductsHandler>();
        services.AddScoped<GetNeedsAttentionHandler>();
        services.AddScoped<GetSignInCodeSmsHandler>();
        services.AddScoped<GetMembersReportHandler>();
        services.AddScoped<GetAbsenceSmsEffectHandler>();
        services.AddScoped<GetTodaySummaryHandler>();

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

        services.AddScoped<RegisterPayableHandler>();
        services.AddScoped<UpdatePayableHandler>();
        services.AddScoped<MarkPayablePaidHandler>();
        services.AddScoped<RevertPayableHandler>();
        services.AddScoped<CancelPayableHandler>();
        services.AddScoped<GetPayableHandler>();
        services.AddScoped<ListPayablesHandler>();
        services.AddScoped<ListPayablesDueSoonHandler>();

        services.AddScoped<GetSmsSettingsHandler>();
        services.AddScoped<UpdateSmsSettingsHandler>();
        services.AddScoped<SmsBatchSender>();
        services.AddScoped<SendDailySmsHandler>();
        services.AddScoped<DailyReportCandidate>();
        services.AddScoped<DebtReminderCandidate>();
        services.AddScoped<AbsenceCandidate>();
        services.AddScoped<SubscriptionReceipts>();
        services.AddScoped<SendSubscriptionReceiptHandler>();
        services.AddScoped<Welcomes>();
        services.AddScoped<SendWelcomeHandler>();
        services.AddScoped<WelcomeBacks>();
        services.AddScoped<SendWelcomeBackHandler>();
        services.AddScoped<TrainerRequestSms>();
        services.AddScoped<SendTrainerRequestSmsHandler>();
        services.AddScoped<CheckSmsDeliveryHandler>();
        services.AddScoped<GetSmsCreditHandler>();
        services.AddScoped<GetSmsSamplesHandler>();
        services.AddScoped<ListSmsMessagesHandler>();
        services.AddScoped<ResendSmsHandler>();
        services.AddScoped<ResendCancelledSmsHandler>();
        services.AddScoped<BroadcastRecipients>();
        services.AddScoped<BroadcastSending>();
        services.AddScoped<GetBroadcastAudienceHandler>();
        services.AddScoped<PreviewBroadcastHandler>();
        services.AddScoped<SendBroadcastHandler>();
        services.AddScoped<ResumeBroadcastHandler>();
        services.AddScoped<ListBroadcastsHandler>();
        services.AddScoped<RunBroadcastHandler>();
        services.AddScoped<RecoverBroadcastsHandler>();

        services.AddScoped<ListAuditLogsHandler>();
        services.AddScoped<ListAuditUsersHandler>();

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
