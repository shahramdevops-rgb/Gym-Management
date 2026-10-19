using System.Globalization;

using FluentValidation;

using Gym.Application.Attendances;
using Gym.Application.Attendances.AutoCheckout;
using Gym.Application.Attendances.CheckIn;
using Gym.Application.Attendances.CheckOut;
using Gym.Application.Attendances.GuestCheckIn;
using Gym.Application.Attendances.SettleGuestVisit;
using Gym.Application.Cafe;
using Gym.Application.Cafe.CreateCafeOrder;
using Gym.Application.Cafe.CreateProduct;
using Gym.Application.Cafe.CreateProductCategory;
using Gym.Application.Common;
using Gym.Application.Expenses;
using Gym.Application.Expenses.RecordExpense;
using Gym.Application.Members;
using Gym.Application.Members.CreateMember;
using Gym.Application.Members.SetMemberActive;
using Gym.Application.Notifications;
using Gym.Application.Notifications.GetSmsSettings;
using Gym.Application.Notifications.SendDailySms;
using Gym.Application.Notifications.UpdateSmsSettings;
using Gym.Application.Payables;
using Gym.Application.Payables.MarkPayablePaid;
using Gym.Application.Payables.RegisterPayable;
using Gym.Application.Payments;
using Gym.Application.Payments.RegisterPayment;
using Gym.Application.Payments.SettleMemberDebt;
using Gym.Application.Pricing;
using Gym.Application.Pricing.GetPrices;
using Gym.Application.Pricing.UpdatePrices;
using Gym.Application.ServiceCharges;
using Gym.Application.ServiceCharges.RecordServiceCharge;
using Gym.Application.ServiceCharges.RecordShopSale;
using Gym.Application.Staff;
using Gym.Application.Staff.CreateStaff;
using Gym.Application.Subscriptions;
using Gym.Application.Subscriptions.AssignSubscription;
using Gym.Application.Subscriptions.CancelSubscription;
using Gym.Application.Subscriptions.FreezeSubscription;
using Gym.Application.Subscriptions.RenewSubscription;
using Gym.Domain.Common;
using Gym.Domain.Expenses;
using Gym.Domain.Notifications;
using Gym.Domain.Payables;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gym.Api.Admin.Demo;

/// <summary>
/// <c>admin seed-demo</c>: fills an empty database with about three months of a made-up gym, for
/// trying the app and for the README's screenshots (roadmap Phase 12).
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything goes through the real handlers</b>, the ones the endpoints call, on a clock the
/// seeder moves day by day (<see cref="DemoClock"/>). So the demo obeys every rule the desk does: a
/// plan's days follow from its sessions, a queued plan moves up when the one before runs out, a
/// guest settles before leaving, the night's job closes forgotten visits. Nothing is inserted
/// behind the rules' back, and a rule that changes changes the demo with it.
/// </para>
/// <para>
/// <b>It refuses anything but an empty database</b> (only the Owner, the lockers and the expense
/// categories the migrations add), and on a Production host it also needs <c>Demo:Allowed</c>,
/// which only the screenshot stack sets. The gym's server has real members, so it is refused
/// there twice over.
/// </para>
/// <para>
/// The randomness is seeded, so two runs on the same day make the same gym. The data is dated
/// back from the moment it runs, so the last day is always today, with members inside right now.
/// </para>
/// </remarks>
public sealed class DemoSeeder
{
    public const string Command = "seed-demo";

    /// <summary>Lets the demo run on a Production host (the screenshot stack). Never set on the gym's server.</summary>
    public const string AllowedSetting = "Demo:Allowed";

    public const int DefaultDays = 90;

    private const decimal SessionPrice = 150_000m;
    private const decimal SingleVisitPrice = 220_000m;
    private const decimal RaisedSessionPrice = 165_000m;
    private const decimal RaisedSingleVisitPrice = 240_000m;
    private const decimal CardioPrice = 60_000m;
    private const int InitialMembers = 40;

    /// <summary>Sessions per plan, as the desk might sell them (5 to 140, BUSINESS_RULES.md §3).</summary>
    private static readonly int[] PlanSizes = [8, 12, 12, 16, 20, 24, 30];

    /// <summary>The first members' birthdays: two today, one in two days, one in four.</summary>
    private static readonly int[] BirthdaysFromToday = [0, 0, 2, 4];

    private readonly IServiceProvider _services;
    private readonly DemoClock _clock;
    private readonly DemoActor _actor;
    private readonly TimeZoneInfo _zone;
    private readonly int _days;
    private readonly DateOnly _today;
    private readonly DateOnly _first;
    private readonly Guid _ownerId;
    private readonly IReadOnlyDictionary<string, Guid> _expenseCategories;
    private readonly List<Guid> _freeLockers;
    private readonly CancellationToken _cancellationToken;

    private readonly Random _random = new(1405);
    private readonly PersianCalendar _jalali = new();
    private readonly List<DemoMember> _members = [];
    private readonly HashSet<Visit> _open = [];
    private readonly List<(Guid Id, decimal Price)> _products = [];
    private readonly List<(Guid Id, DateOnly DueDate)> _payables = [];
    private readonly List<(Guid Id, string FullName)> _staff = [];
    private readonly Queue<string> _names;
    private readonly HashSet<string> _phones = [];
    private bool _smsOn;
    private SubscriptionResponse? _mistake;

    private DemoSeeder(
        IServiceProvider services,
        DemoClock clock,
        DemoActor actor,
        TimeZoneInfo zone,
        int days,
        Guid ownerId,
        IReadOnlyDictionary<string, Guid> expenseCategories,
        List<Guid> lockers,
        CancellationToken cancellationToken)
    {
        _services = services;
        _clock = clock;
        _actor = actor;
        _zone = zone;
        _days = days;
        _ownerId = ownerId;
        _expenseCategories = expenseCategories;
        _freeLockers = lockers;
        _cancellationToken = cancellationToken;

        _today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.RealStart, zone).DateTime);
        _first = _today.AddDays(-days);

        var names = DemoNames.FirstNames
            .SelectMany(first => DemoNames.LastNames.Select(last => $"{first} {last}"))
            .OrderBy(_ => _random.Next())
            .ToList();
        _names = new Queue<string>(names);
    }

    /// <summary>
    /// Runs the demo over <paramref name="days"/> days ending now, or refuses with the reason. The
    /// host must have been built with <see cref="DemoServices.AddDemoSeedServices"/>.
    /// </summary>
    /// <returns>A one-line summary of what was made.</returns>
    public static async Task<Result<string>> RunAsync(IServiceProvider services, int days, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);

        var clock = services.GetRequiredService<TimeProvider>() as DemoClock;
        var actor = services.GetRequiredService<ICurrentUser>() as DemoActor;
        if (clock is null || actor is null)
        {
            throw new InvalidOperationException("seed-demo needs the demo services: see DemoServices.AddDemoSeedServices in Program.cs.");
        }

        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var allowed = provider.GetRequiredService<IConfiguration>().GetValue<bool>(AllowedSetting);
        if (!IsAllowedHere(provider.GetRequiredService<IHostEnvironment>(), allowed))
        {
            return Result.Failure<string>(DemoErrors.NotAllowedHere);
        }

        var users = provider.GetRequiredService<UserManager<User>>();
        var owners = await users.GetUsersInRoleAsync(Roles.Owner);
        if (owners.Count != 1)
        {
            return Result.Failure<string>(DemoErrors.NoOwner);
        }

        var db = provider.GetRequiredService<IAppDbContext>();
        if (!await IsEmptyAsync(db, users, cancellationToken))
        {
            return Result.Failure<string>(DemoErrors.DatabaseNotEmpty);
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(provider.GetRequiredService<IOptions<GymCalendarOptions>>().Value.TimeZone);
        var categories = await db.ExpenseCategories.AsNoTracking().ToDictionaryAsync(c => c.Name, c => c.Id, cancellationToken);
        var lockers = await db.Lockers.AsNoTracking()
            .Where(l => !l.IsOutOfService)
            .OrderBy(l => l.Number)
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        var seeder = new DemoSeeder(services, clock, actor, zone, days, owners[0].Id, categories, lockers, cancellationToken);
        await seeder.SeedAsync();

        return await seeder.SummaryAsync();
    }

    /// <summary>
    /// Anywhere but Production, and on Production only with <see cref="AllowedSetting"/> on: the
    /// screenshot stack runs the production images, the gym's server never sets it.
    /// </summary>
    public static bool IsAllowedHere(IHostEnvironment environment, bool allowed)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return !environment.IsProduction() || allowed;
    }

    /// <summary>Nothing but what the migrations and the Owner seeding put there.</summary>
    private static async Task<bool> IsEmptyAsync(IAppDbContext db, UserManager<User> users, CancellationToken cancellationToken)
    {
        if ((await users.GetUsersInRoleAsync(Roles.Staff)).Count > 0)
        {
            return false;
        }

        return !await db.Members.AnyAsync(cancellationToken)
            && !await db.Attendances.AnyAsync(cancellationToken)
            && !await db.Payments.AnyAsync(cancellationToken)
            && !await db.ServiceCharges.AnyAsync(cancellationToken)
            && !await db.CafeOrders.AnyAsync(cancellationToken)
            && !await db.ProductCategories.AnyAsync(cancellationToken)
            && !await db.Expenses.AnyAsync(cancellationToken)
            && !await db.Payables.AnyAsync(cancellationToken)
            && !await db.PriceLists.AnyAsync(p => p.SessionPrice != null || p.SingleVisitPrice != null, cancellationToken);
    }

    private async Task SeedAsync()
    {
        PlanMembers();

        for (var day = _first; day <= _today; day = day.AddDays(1))
        {
            var events = DayEvents(day)
                .Where(e => e.At < _clock.RealStart)
                .OrderBy(e => e.At)
                .ThenBy(e => e.Order)
                .ToList();

            foreach (var e in events)
            {
                _clock.Set(e.At);
                await e.Run();
            }
        }
    }

    private async Task<string> SummaryAsync()
    {
        var (members, visits, payments) = await ReadAsync(async db => (
            await db.Members.CountAsync(_cancellationToken),
            await db.Attendances.CountAsync(_cancellationToken),
            await db.Payments.CountAsync(_cancellationToken)));

        // Invariant, like the logs: a machine set to Persian would otherwise write this date in its own calendar.
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Seeded {_days} days up to {_today:yyyy-MM-dd}: {members} members, {visits} visits, {payments} payments.");
    }

    // ---------------------------------------------------------------- the people

    private void PlanMembers()
    {
        var count = InitialMembers;
        var joinDays = new List<int>();
        for (var i = 0; i < InitialMembers; i++)
        {
            joinDays.Add(_random.Next(0, Math.Min(7, _days + 1)));
        }

        for (var day = 7; day <= _days; day++)
        {
            if (_random.NextDouble() < 0.4)
            {
                joinDays.Add(day);
                count++;
            }
        }

        for (var i = 0; i < count; i++)
        {
            var roll = _random.NextDouble();
            var habit = roll < 0.12 ? Habit.SingleVisits : roll < 0.22 ? Habit.PaysLater : Habit.Regular;
            var joinDay = joinDays[i];
            int? leaveDay = _random.NextDouble() < 0.15 && joinDay + 20 < _days - 3
                ? _random.Next(joinDay + 20, _days - 3)
                : null;

            _members.Add(new DemoMember
            {
                FullName = _names.Dequeue(),
                PhoneNumber = NewPhone(),
                BirthDate = BirthDate(i),
                Notes = i < DemoNames.MemberNotes.Length * 5 && i % 5 == 4 ? DemoNames.MemberNotes[i / 5] : null,
                Habit = habit,
                VisitChance = habit == Habit.SingleVisits ? 0.1 + (_random.NextDouble() * 0.15) : 0.35 + (_random.NextDouble() * 0.35),
                Sessions = PlanSizes[_random.Next(PlanSizes.Length)],
                JoinDay = joinDay,
                LeaveDay = leaveDay,
            });
        }
    }

    private string NewPhone()
    {
        while (true)
        {
            var prefix = DemoNames.MobilePrefixes[_random.Next(DemoNames.MobilePrefixes.Length)];
            var phone = string.Create(CultureInfo.InvariantCulture, $"{prefix}{_random.Next(1_000_000, 10_000_000)}");
            if (_phones.Add(phone))
            {
                return phone;
            }
        }
    }

    /// <summary>
    /// The first few members have a birthday today or in the next days (by the Jalali calendar, as
    /// the desk panel counts it), so the desk panel and the birthday SMS have someone to show.
    /// </summary>
    private DateOnly BirthDate(int index)
    {
        var year = _random.Next(1360, 1386);
        int month;
        int day;
        if (index < BirthdaysFromToday.Length)
        {
            var date = _today.AddDays(BirthdaysFromToday[index]).ToDateTime(TimeOnly.MinValue);
            month = _jalali.GetMonth(date);
            day = _jalali.GetDayOfMonth(date);
            while (_jalali.GetDaysInMonth(year, month) < day)
            {
                year++;
            }
        }
        else
        {
            month = _random.Next(1, 13);
            day = _random.Next(1, 30);
        }

        return DateOnly.FromDateTime(_jalali.ToDateTime(year, month, day, 0, 0, 0, 0));
    }

    // ---------------------------------------------------------------- one day

    private List<DemoEvent> DayEvents(DateOnly day)
    {
        var index = day.DayNumber - _first.DayNumber;
        var events = new List<DemoEvent>();
        void Add(DateTimeOffset at, Func<Task> run) => events.Add(new DemoEvent(at, events.Count, run));

        // Gym:ClosingTime is midnight: the night's job closes what the desk left open.
        if (index > 0)
        {
            Add(At(day, 0, 1), AutoCheckoutAsync);
        }

        if (index == 0)
        {
            Add(At(day, 6, 30), SetUpGymAsync);
        }

        if (index == _days * 5 / 9 && index > 0)
        {
            Add(At(day, 8, 0), () => SetPricesAsync(RaisedSessionPrice, RaisedSingleVisitPrice));
        }

        if (index == _days * 2 / 3 && index > 0)
        {
            Add(At(day, 8, 15), TurnSmsOnAsync);
        }

        if (_smsOn)
        {
            Add(At(day, 10, 30), SendDailySmsAsync);
            Add(At(day, 22, 45), SendDailyReportAsync);
        }

        AddExpenses(day, index, Add);

        foreach (var payable in _payables.Where(p => p.DueDate == day && day < _today))
        {
            Add(At(day, 11, 0), () => MarkPaidAsync(payable.Id));
        }

        if (index == _days * 4 / 9 && index > 0)
        {
            Add(At(day, 12, 0), SellByMistakeAsync);
            Add(At(day, 12, 20), PutTheMistakeRightAsync);
        }

        if (index == _days - 12 && index > 0)
        {
            Add(At(day, 13, 0), FreezeSomeoneAsync);
        }

        if (index == _days - 8 && index > 0)
        {
            Add(At(day, 13, 30), () => DeactivateSomeoneAsync(index));
        }

        foreach (var visit in DayVisits(day, index))
        {
            Add(visit.In, () => CheckInAsync(visit, index));
            if (!visit.StaysOpen)
            {
                Add(visit.Out, () => CheckOutAsync(visit));
            }
        }

        return events;
    }

    private List<Visit> DayVisits(DateOnly day, int index)
    {
        var visits = new List<Visit>();
        var friday = day.DayOfWeek == DayOfWeek.Friday;

        foreach (var member in _members)
        {
            if (member.JoinDay > index || member.LeaveDay <= index || member.Frozen || member.Deactivated)
            {
                continue;
            }

            var chance = friday ? member.VisitChance * 0.4 : member.VisitChance;
            if (member.JoinDay == index || _random.NextDouble() < chance)
            {
                visits.Add(NewVisit(day, index, member, guestName: null));
            }
        }

        if (_random.NextDouble() < 0.25)
        {
            visits.Add(NewVisit(day, index, member: null, guestName: _names.Dequeue()));
        }

        if (day == _today)
        {
            KeepSomeInside(visits);
        }

        return visits;
    }

    private Visit NewVisit(DateOnly day, int index, DemoMember? member, string? guestName)
    {
        // Mornings, a quiet middle of the day, and the evening rush.
        var roll = _random.NextDouble();
        var minute = roll < 0.3 ? _random.Next(7 * 60, 11 * 60)
            : roll < 0.5 ? _random.Next(11 * 60, 17 * 60)
            : _random.Next(17 * 60, 22 * 60);

        // On the first day the prices are set at 06:30, before the doors open.
        if (index == 0)
        {
            minute = Math.Max(minute, 7 * 60);
        }

        var stay = Math.Min(minute + _random.Next(50, 121), (23 * 60) + 50);

        return new Visit
        {
            Member = member,
            GuestName = guestName,
            In = At(day, minute / 60, minute % 60),
            Out = At(day, stay / 60, stay % 60),
            // A few members walk out without the desk checking them out; the night's job closes those.
            Forgotten = member is not null && _random.NextDouble() < 0.03,
        };
    }

    /// <summary>
    /// Today's visits that would start after now start in the last two and a half hours instead,
    /// and are still going: the board and the locker map show people inside.
    /// </summary>
    private void KeepSomeInside(List<Visit> visits)
    {
        const int Inside = 12;
        var now = _clock.RealStart;
        var from = Max(now.AddMinutes(-150), At(_today, 0, 5));
        var to = now.AddMinutes(-3);

        // Started already and not over yet: inside, as they would be.
        var going = visits.Where(v => v.In < to && v.Out >= now.AddMinutes(-1)).ToList();
        going.ForEach(v => v.StaysOpen = true);

        // Not started yet: a few come forward to make up the number, the rest never happen.
        var later = visits.Where(v => v.In >= to).ToList();
        foreach (var visit in later.Take(Math.Max(0, Inside - going.Count)))
        {
            visit.In = to > from ? from.AddSeconds(_random.Next(0, (int)(to - from).TotalSeconds)) : to;
            visit.StaysOpen = true;
        }

        visits.RemoveAll(v => v.In >= to);
    }

    // ---------------------------------------------------------------- the office

    private async Task SetUpGymAsync()
    {
        ActAsOwner();
        await SetPricesAsync(SessionPrice, SingleVisitPrice);

        foreach (var (userName, fullName, password) in DemoNames.Staff)
        {
            var staff = await MustAsync<CreateStaffHandler, StaffResponse>(
                async h => await h.Handle(await ValidAsync(new CreateStaffCommand(userName, fullName, password)), _cancellationToken));
            _staff.Add((staff.Id, staff.FullName));
        }

        foreach (var (category, products) in DemoNames.Menu)
        {
            var created = await MustAsync<CreateProductCategoryHandler, ProductCategoryResponse>(
                async h => await h.Handle(await ValidAsync(new CreateProductCategoryCommand(category)), _cancellationToken));

            foreach (var (name, price) in products)
            {
                var product = await MustAsync<CreateProductHandler, ProductResponse>(
                    async h => await h.Handle(await ValidAsync(new CreateProductCommand(name, created.Id, price)), _cancellationToken));
                _products.Add((product.Id, price));
            }
        }

        // Cheques for the machines bought before the system started, one due in two days so the
        // dashboard has something coming up; and a small loan paid in instalments.
        DateOnly[] chequeDates = [_first.AddDays(15), _first.AddDays(45), _first.AddDays(75), _today.AddDays(2)];
        for (var i = 0; i < chequeDates.Length; i++)
        {
            await RegisterPayableAsync(new RegisterPayableCommand(
                PayableKind.Cheque,
                15_000_000m,
                chequeDates[i],
                "فروشگاه تجهیزات ورزشی نیرو",
                $"دستگاه پرس پا و اسکات، چک {PersianDigits(i + 1)} از {PersianDigits(chequeDates.Length)}",
                _expenseCategories["تجهیزات"]));
        }

        const int Installments = 6;
        for (var i = 0; i < Installments; i++)
        {
            await RegisterPayableAsync(new RegisterPayableCommand(
                PayableKind.Installment,
                5_000_000m,
                _first.AddDays(25 + (30 * i)),
                "صندوق قرض‌الحسنهٔ محله",
                "وام بازسازی رختکن",
                _expenseCategories["تعمیر و نگهداری"],
                i + 1,
                Installments));
        }
    }

    private async Task RegisterPayableAsync(RegisterPayableCommand command)
    {
        var payable = await MustAsync<RegisterPayableHandler, PayableResponse>(
            async h => await h.Handle(await ValidAsync(command), _cancellationToken));
        _payables.Add((payable.Id, payable.DueDate));
    }

    private async Task MarkPaidAsync(Guid payableId)
    {
        ActAsOwner();
        await MustAsync<MarkPayablePaidHandler, PayableResponse>(h => h.Handle(payableId, _cancellationToken));
    }

    private async Task SetPricesAsync(decimal sessionPrice, decimal singleVisitPrice)
    {
        ActAsOwner();
        uint version;
        await using (var scope = _services.CreateAsyncScope())
        {
            version = (await scope.ServiceProvider.GetRequiredService<GetPricesHandler>().Handle(_cancellationToken)).Version;
        }

        await MustAsync<UpdatePricesHandler, PricesResponse>(
            async h => await h.Handle(await ValidAsync(new UpdatePricesCommand(sessionPrice, singleVisitPrice, version)), _cancellationToken));
    }

    private async Task TurnSmsOnAsync()
    {
        ActAsOwner();
        uint version;
        await using (var scope = _services.CreateAsyncScope())
        {
            version = (await scope.ServiceProvider.GetRequiredService<GetSmsSettingsHandler>().Handle(_cancellationToken)).Version;
        }

        var command = new UpdateSmsSettingsCommand(
            Enabled: true,
            SubscriptionExpiring: new SmsKindSettings(true, 3, new TimeOnly(10, 0)),
            LowSessions: new SmsKindSettings(true, 2, new TimeOnly(10, 15)),
            Birthday: new SmsKindSettings(true, 0, new TimeOnly(9, 0)),
            PayableDue: new SmsKindSettings(true, 3, new TimeOnly(9, 30)),
            OwnerPhone: "09121112233",
            DailyReport: new DailyReportSettings(true, "09121112233"),
            Version: version);

        await MustAsync<UpdateSmsSettingsHandler, SmsSettingsResponse>(
            async h => await h.Handle(await ValidAsync(command), _cancellationToken));
        _smsOn = true;
    }

    /// <summary>The daily runs, as Hangfire would make them, on nobody's behalf. The report has its own time.</summary>
    private async Task SendDailySmsAsync()
    {
        _actor.ActAsNobody();
        foreach (var kind in Enum.GetValues<NotificationKind>().Where(kind => kind != NotificationKind.DailyReport))
        {
            await using var scope = _services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<SendDailySmsHandler>().Handle(kind, _cancellationToken);
        }
    }

    /// <summary>The Owner's daily report at 22:45 (task 10.7), as Hangfire would make it.</summary>
    private async Task SendDailyReportAsync()
    {
        _actor.ActAsNobody();
        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SendDailySmsHandler>().Handle(NotificationKind.DailyReport, _cancellationToken);
    }

    private void AddExpenses(DateOnly day, int index, Action<DateTimeOffset, Func<Task>> add)
    {
        var date = day.ToDateTime(TimeOnly.MinValue);
        var dayOfMonth = _jalali.GetDayOfMonth(date);
        var month = DemoNames.JalaliMonths[_jalali.GetMonth(date) - 1];
        var lastMonth = DemoNames.JalaliMonths[(_jalali.GetMonth(date) + 10) % 12];

        void Expense(int hour, string category, decimal amount, string description) =>
            add(At(day, hour, 0), () => RecordExpenseAsync(day, category, amount, description));

        if (dayOfMonth == 1)
        {
            Expense(9, "اجاره", 30_000_000m, $"اجارهٔ سالن، {month}");
        }

        // Salaries at the end of the month they are for.
        if (dayOfMonth == _jalali.GetDaysInMonth(_jalali.GetYear(date), _jalali.GetMonth(date)))
        {
            foreach (var staff in DemoNames.Staff)
            {
                Expense(20, "حقوق", 15_000_000m, $"حقوق {staff.FullName}، {month}");
            }
        }

        if (dayOfMonth == 5)
        {
            Expense(9, "برق", Money(3_200_000, 4_600_000), $"قبض برق {lastMonth}");
        }

        if (dayOfMonth == 9)
        {
            Expense(9, "آب", Money(900_000, 1_400_000), $"قبض آب {lastMonth}");
        }

        if (index % 7 == 3)
        {
            add(At(day, 9, 30), () => RecordExpenseAsync(
                day, null, Money(1_200_000, 2_000_000), "خرید نوشیدنی و میان‌وعدهٔ بوفه", ExpenseCategory.CafePurchasingId));
        }

        if (index == _days / 4)
        {
            Expense(10, "تجهیزات", 12_500_000m, "دو جفت دمبل و یک نیمکت شیب‌دار");
        }

        if (index == _days / 3)
        {
            Expense(10, "سایر", 650_000m, "مواد شوینده و دستمال");
        }

        if (index == _days * 3 / 5)
        {
            Expense(10, "تعمیر و نگهداری", 2_800_000m, "سرویس دوره‌ای تردمیل‌ها");
        }
    }

    private async Task RecordExpenseAsync(DateOnly day, string? category, decimal amount, string description, Guid? categoryId = null)
    {
        ActAsOwner();
        var command = new RecordExpenseCommand(amount, categoryId ?? _expenseCategories[category!], day, description, null);
        await MustAsync<RecordExpenseHandler, ExpenseResponse>(
            async h => await h.Handle(await ValidAsync(command), _cancellationToken));
    }

    /// <summary>
    /// The desk sells a second plan to someone who already has one, takes the money, and the Owner
    /// puts it right: the cancellation with the reason, which gives the money back (BUSINESS_RULES.md
    /// §4 Cancel, §5).
    /// </summary>
    private async Task SellByMistakeAsync()
    {
        var member = _members.FirstOrDefault(m => m.Registered && m.Habit == Habit.Regular && !m.Frozen && !m.Deactivated && m.LeaveDay is null);
        if (member is null)
        {
            return;
        }

        ActAsDesk();
        var sold = await MustAsync<AssignSubscriptionHandler, SubscriptionResponse>(
            async h => await h.Handle(member.Id, await ValidAsync(new AssignSubscriptionCommand(member.Sessions)), _cancellationToken));
        await PayAsync(sold.Id, sold.Price, PaymentMethod.Card);
        _mistake = sold;
    }

    private async Task PutTheMistakeRightAsync()
    {
        if (_mistake is not { } sold)
        {
            return;
        }

        ActAsOwner();
        const string Reason = "پلن تکراری؛ عضو پلن فعال داشت و اشتباهی دوباره فروخته شد";
        // Cancelling gives the card payment back itself (BUSINESS_RULES.md §4 Cancel, 6.5.37).
        await MustAsync<CancelSubscriptionHandler, SubscriptionResponse>(async h => await h.Handle(
            sold.Id, await ValidAsync(new CancelSubscriptionCommand(Reason)), _cancellationToken));
    }

    /// <summary>The Owner freezes a member's plan while they travel; they do not come until the end.</summary>
    private async Task FreezeSomeoneAsync()
    {
        var today = Today();
        ActAsOwner();
        foreach (var member in _members.Where(m => m.Registered && m.Habit == Habit.Regular && !m.Frozen && !m.Deactivated && m.LeaveDay is null && !m.IsInside))
        {
            var active = await ReadAsync(db => db.Subscriptions.AsNoTracking()
                .Where(s => s.MemberId == member.Id && !s.IsSingleSession && s.CancelledAt == null && s.FrozenSince == null)
                .Where(s => s.StartDate <= today && s.EndDate >= today && s.UsedSessions < s.TotalSessions)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync(_cancellationToken));

            if (active is { } id && (await TryAsync<FreezeSubscriptionHandler, SubscriptionResponse>(
                    h => h.Handle(id, _cancellationToken))).IsSuccess)
            {
                member.Frozen = true;
                return;
            }
        }
    }

    /// <summary>A member who stopped coming is deactivated by the Owner.</summary>
    private async Task DeactivateSomeoneAsync(int index)
    {
        var member = _members.FirstOrDefault(m => m.Registered && m.LeaveDay < index && !m.Deactivated);
        if (member is null)
        {
            return;
        }

        ActAsOwner();
        await MustAsync<SetMemberActiveHandler, MemberResponse>(h => h.Deactivate(member.Id, _cancellationToken));
        member.Deactivated = true;
    }

    // ---------------------------------------------------------------- the desk

    private async Task CheckInAsync(Visit visit, int index)
    {
        // A day's visits are planned in the morning and the freeze comes at 13:00, so the member frozen
        // that day may still have a visit planned for the evening. They stay away: coming in would
        // unfreeze the plan (BUSINESS_RULES.md §4 Freeze), and the demo would show no frozen plan.
        if (visit.Member is { Frozen: true })
        {
            return;
        }

        ActAsDesk();
        var locker = TakeLocker();

        if (visit.Member is not { } member)
        {
            var guest = await TryAsync<GuestCheckInHandler, AttendanceResponse>(
                async h => await h.Handle(await ValidAsync(new GuestCheckInCommand(visit.GuestName!, locker)), _cancellationToken));
            Opened(visit, locker, guest);
            return;
        }

        if (!member.Registered)
        {
            var created = await MustAsync<CreateMemberHandler, MemberResponse>(async h => await h.Handle(
                await ValidAsync(new CreateMemberCommand(member.FullName, member.PhoneNumber, member.Notes, member.BirthDate)),
                _cancellationToken));
            member.Id = created.Id;
            member.Registered = true;
        }

        // As at the desk: try the plan they have; if nothing is usable, sell in the same box.
        var plain = await TryAsync<CheckInHandler, AttendanceResponse>(
            async h => await h.Handle(member.Id, await ValidAsync(new CheckInCommand(locker)), _cancellationToken));

        if (plain.IsSuccess)
        {
            Opened(visit, locker, plain);
            await PayWhatIsDueAsync(member, index);
            await MaybeRenewEarlyAsync(member, plain.Value.SubscriptionId);
        }
        else
        {
            var sale = member.Habit == Habit.SingleVisits
                ? new CheckInSale(CheckInSaleKind.SingleVisit)
                : new CheckInSale(CheckInSaleKind.Membership, member.Sessions);
            var sold = await TryAsync<CheckInHandler, AttendanceResponse>(
                async h => await h.Handle(member.Id, await ValidAsync(new CheckInCommand(locker, sale)), _cancellationToken));

            // Still refused (a renewal that only starts tomorrow, say): the member goes home.
            if (!Opened(visit, locker, sold))
            {
                return;
            }

            await PayWhatIsDueAsync(member, index);
            await PayForSaleAsync(member, sold.Value.SubscriptionId!.Value, index);
        }

        if (_random.NextDouble() < 0.12)
        {
            var charge = await MustAsync<RecordServiceChargeHandler, ServiceChargeResponse>(async h => await h.Handle(
                visit.AttendanceId!.Value, await ValidAsync(new RecordServiceChargeCommand(ServiceChargeKind.Cardio, CardioPrice)), _cancellationToken));
            if (_random.NextDouble() < 0.9)
            {
                await PayChargeAsync(charge.Id, CardioPrice);
            }
            else
            {
                Owe(member, OwedKind.ServiceCharge, charge.Id, CardioPrice, index);
            }
        }

        if (_random.NextDouble() < 0.02)
        {
            var (description, price) = DemoNames.ShopItems[_random.Next(DemoNames.ShopItems.Length)];
            var charges = await MustAsync<RecordShopSaleHandler, IReadOnlyList<ServiceChargeResponse>>(async h => await h.Handle(
                visit.AttendanceId!.Value, await ValidAsync(new RecordShopSaleCommand([new ShopSaleItem(description, 1, price)])), _cancellationToken));
            foreach (var charge in charges)
            {
                await PayChargeAsync(charge.Id, price);
            }
        }
    }

    private bool Opened(Visit visit, Guid? locker, Result<AttendanceResponse> result)
    {
        if (result.IsFailure)
        {
            ReleaseLocker(locker);
            return false;
        }

        visit.AttendanceId = result.Value.Id;
        visit.Locker = locker;
        _open.Add(visit);
        if (visit.Member is { } member)
        {
            member.IsInside = true;
        }

        return true;
    }

    private async Task CheckOutAsync(Visit visit)
    {
        if (visit.AttendanceId is not { } attendanceId || !_open.Contains(visit) || visit.Forgotten)
        {
            return;
        }

        ActAsDesk();

        // A drink on the way out. A member's may go on their account; a guest's is settled first.
        var order = _random.NextDouble() < (visit.Member is null ? 0.5 : 0.3);
        if (order)
        {
            var lines = _products.OrderBy(_ => _random.Next()).Take(_random.Next(1, 3)).ToList();
            var total = lines.Sum(l => l.Price);
            var payNow = visit.Member is not null && _random.NextDouble() < 0.9;
            var command = new CreateCafeOrderCommand(
                visit.Member?.Id,
                lines.Select(l => new CafeOrderLine(l.Id, 1)).ToList(),
                payNow ? new CafeOrderPayment(total, Method(), null) : null,
                attendanceId);
            var created = await MustAsync<CreateCafeOrderHandler, CafeOrderResponse>(
                async h => await h.Handle(await ValidAsync(command), _cancellationToken));
            if (visit.Member is { } member && !payNow)
            {
                Owe(member, OwedKind.CafeOrder, created.Id, total, Today().DayNumber - _first.DayNumber);
            }

            if (visit.Member is null)
            {
                await MustAsync<SettleGuestVisitHandler, SettlementResponse>(async h => await h.Handle(
                    attendanceId, await ValidAsync(new SettleGuestVisitCommand(total, Method(), null)), _cancellationToken));
            }
        }

        await MustAsync<CheckOutHandler, AttendanceResponse>(h => h.Handle(attendanceId, _cancellationToken));
        Closed(visit);
    }

    private void Closed(Visit visit)
    {
        _open.Remove(visit);
        ReleaseLocker(visit.Locker);
        if (visit.Member is { } member)
        {
            member.IsInside = false;
        }
    }

    private async Task AutoCheckoutAsync()
    {
        _actor.ActAsNobody();
        await using (var scope = _services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AutoCheckoutHandler>().Handle(_cancellationToken);
        }

        foreach (var visit in _open.ToList())
        {
            Closed(visit);
        }
    }

    private async Task PayForSaleAsync(DemoMember member, Guid subscriptionId, int index)
    {
        var price = await ReadAsync(db => db.Subscriptions.AsNoTracking()
            .Where(s => s.Id == subscriptionId)
            .Select(s => s.Price)
            .SingleAsync(_cancellationToken));

        // Most pay in full; some pay half and the rest a week or so later.
        var now = member.Habit == Habit.PaysLater ? Math.Floor(price / 2 / 10_000m) * 10_000m : price;
        await PayAsync(subscriptionId, now, Method());

        if (price > now)
        {
            Owe(member, OwedKind.Subscription, subscriptionId, price - now, index + _random.Next(4, 11));
        }
    }

    /// <summary>
    /// Something left on the member's account, paid on their first visit from <paramref name="payOnDay"/>
    /// on. One in ten is never paid, which is what the receivables are for.
    /// </summary>
    private void Owe(DemoMember member, OwedKind kind, Guid id, decimal amount, int payOnDay)
    {
        if (_random.NextDouble() >= 0.1)
        {
            member.Owed.Add(new Owed(kind, id, amount, Math.Max(payOnDay, 0) + 1));
        }
    }

    private async Task PayWhatIsDueAsync(DemoMember member, int index)
    {
        foreach (var owed in member.Owed.Where(o => o.PayOnDay <= index).ToList())
        {
            switch (owed.Kind)
            {
                case OwedKind.Subscription:
                    await PayAsync(owed.Id, owed.Amount, Method());
                    break;
                case OwedKind.ServiceCharge:
                    await PayChargeAsync(owed.Id, owed.Amount);
                    break;
                case OwedKind.CafeOrder:
                    await MustAsync<RegisterCafeOrderPaymentHandler, PaymentResponse>(async h => await h.Handle(
                        owed.Id, await ValidAsync(new RegisterPaymentCommand(owed.Amount, Method(), null)), _cancellationToken));
                    break;
            }

            member.Owed.Remove(owed);
        }
    }

    /// <summary>Some members renew on their last session, before the plan runs out.</summary>
    private async Task MaybeRenewEarlyAsync(DemoMember member, Guid? usedSubscriptionId)
    {
        if (member.Habit != Habit.Regular || usedSubscriptionId is not { } id || _random.NextDouble() >= 0.35)
        {
            return;
        }

        var today = Today();
        var (remaining, singleSession, queued) = await ReadAsync(async db => (
            await db.Subscriptions.Where(s => s.Id == id).Select(s => s.TotalSessions - s.UsedSessions).SingleAsync(_cancellationToken),
            await db.Subscriptions.Where(s => s.Id == id).Select(s => s.IsSingleSession).SingleAsync(_cancellationToken),
            await db.Subscriptions.AnyAsync(s => s.MemberId == member.Id && s.CancelledAt == null && s.StartDate > today, _cancellationToken)));

        if (singleSession || queued || remaining > 1)
        {
            return;
        }

        var renewed = await MustAsync<RenewSubscriptionHandler, SubscriptionResponse>(
            h => h.Handle(member.Id, _cancellationToken));
        await PayAsync(renewed.Id, renewed.Price, Method());
    }

    private async Task PayAsync(Guid subscriptionId, decimal amount, PaymentMethod method) =>
        await MustAsync<RegisterPaymentHandler, PaymentResponse>(async h => await h.Handle(
            subscriptionId, await ValidAsync(new RegisterPaymentCommand(amount, method, null)), _cancellationToken));

    private async Task PayChargeAsync(Guid chargeId, decimal amount) =>
        await MustAsync<RegisterServiceChargePaymentHandler, PaymentResponse>(async h => await h.Handle(
            chargeId, await ValidAsync(new RegisterPaymentCommand(amount, Method(), null)), _cancellationToken));

    private Guid? TakeLocker()
    {
        // Every locker taken: a reserve place (BUSINESS_RULES.md §6).
        if (_freeLockers.Count == 0)
        {
            return null;
        }

        var index = _random.Next(_freeLockers.Count);
        var locker = _freeLockers[index];
        _freeLockers.RemoveAt(index);

        return locker;
    }

    private void ReleaseLocker(Guid? locker)
    {
        if (locker is { } id)
        {
            _freeLockers.Add(id);
        }
    }

    // ---------------------------------------------------------------- helpers

    private void ActAsOwner() => _actor.ActAs(_ownerId, isOwner: true);

    /// <summary>The morning member of staff until three, the evening one after; now and then the Owner.</summary>
    private void ActAsDesk()
    {
        if (_staff.Count == 0 || _random.NextDouble() < 0.1)
        {
            ActAsOwner();
            return;
        }

        var hour = TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), _zone).Hour;
        _actor.ActAs(_staff[hour < 15 ? 0 : 1].Id, isOwner: false);
    }

    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), _zone).DateTime);

    private DateTimeOffset At(DateOnly day, int hour, int minute) =>
        new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(hour, minute)), _zone));

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    /// <summary>A number inside Persian text, written the way the desk would type it.</summary>
    private static string PersianDigits(int number) =>
        string.Concat(number.ToString(CultureInfo.InvariantCulture).Select(digit => (char)('۰' + (digit - '0'))));

    /// <summary>Mostly card, as at a real desk; some transfers and some cash.</summary>
    private PaymentMethod Method()
    {
        var roll = _random.NextDouble();

        return roll < 0.7 ? PaymentMethod.Card : roll < 0.85 ? PaymentMethod.BankTransfer : PaymentMethod.Cash;
    }

    /// <summary>A round amount between the two, to the nearest ten thousand.</summary>
    private decimal Money(int min, int max) => Math.Round(_random.Next(min, max) / 10_000m) * 10_000m;

    /// <summary>Runs one step in its own scope, as one request would, and expects it to succeed.</summary>
    private async Task<T> MustAsync<THandler, T>(Func<THandler, Task<Result<T>>> step)
        where THandler : notnull
    {
        var result = await TryAsync(step);

        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"A demo step was refused at {_clock.GetUtcNow():O}: {result.Error.Code}: {result.Error.Description}");
    }

    private async Task<Result<T>> TryAsync<THandler, T>(Func<THandler, Task<Result<T>>> step)
        where THandler : notnull
    {
        await using var scope = _services.CreateAsyncScope();

        return await step(scope.ServiceProvider.GetRequiredService<THandler>());
    }

    /// <summary>The endpoint's validation, which calling a handler directly would otherwise skip.</summary>
    private async Task<TCommand> ValidAsync<TCommand>(TCommand command)
    {
        await using var scope = _services.CreateAsyncScope();
        if (scope.ServiceProvider.GetService<IValidator<TCommand>>() is { } validator)
        {
            await validator.ValidateAndThrowAsync(command, _cancellationToken);
        }

        return command;
    }

    private async Task<T> ReadAsync<T>(Func<IAppDbContext, Task<T>> read)
    {
        await using var scope = _services.CreateAsyncScope();

        return await read(scope.ServiceProvider.GetRequiredService<IAppDbContext>());
    }

    private enum Habit
    {
        Regular,
        SingleVisits,
        PaysLater,
    }

    private enum OwedKind
    {
        Subscription,
        ServiceCharge,
        CafeOrder,
    }

    private sealed record Owed(OwedKind Kind, Guid Id, decimal Amount, int PayOnDay);

    private sealed record DemoEvent(DateTimeOffset At, int Order, Func<Task> Run);

    private sealed class DemoMember
    {
        public required string FullName { get; init; }

        public required string PhoneNumber { get; init; }

        public required DateOnly BirthDate { get; init; }

        public string? Notes { get; init; }

        public required Habit Habit { get; init; }

        public required double VisitChance { get; init; }

        public required int Sessions { get; init; }

        public required int JoinDay { get; init; }

        public int? LeaveDay { get; init; }

        public Guid Id { get; set; }

        public bool Registered { get; set; }

        public bool Frozen { get; set; }

        public bool Deactivated { get; set; }

        public bool IsInside { get; set; }

        public List<Owed> Owed { get; } = [];
    }

    private sealed class Visit
    {
        public DemoMember? Member { get; init; }

        public string? GuestName { get; init; }

        public DateTimeOffset In { get; set; }

        public DateTimeOffset Out { get; init; }

        public bool Forgotten { get; init; }

        public bool StaysOpen { get; set; }

        public Guid? AttendanceId { get; set; }

        public Guid? Locker { get; set; }
    }
}
