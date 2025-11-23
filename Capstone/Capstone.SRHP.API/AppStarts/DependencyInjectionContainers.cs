using Capstone.HPTY.API.Services;
using Capstone.HPTY.API.SideServices;
using Capstone.HPTY.RepositoryLayer;
using Capstone.HPTY.RepositoryLayer.Repositories;
using Capstone.HPTY.RepositoryLayer.UnitOfWork;
using Capstone.HPTY.ServiceLayer.Extensions;
using Capstone.HPTY.ServiceLayer.Interfaces.BackgroundService;
using Capstone.HPTY.ServiceLayer.Interfaces.ChatService;
using Capstone.HPTY.ServiceLayer.Interfaces.ComboService;
using Capstone.HPTY.ServiceLayer.Interfaces.Customer;
using Capstone.HPTY.ServiceLayer.Interfaces.FeedbackService;
using Capstone.HPTY.ServiceLayer.Interfaces.HotpotService;
using Capstone.HPTY.ServiceLayer.Interfaces.ManagerService;
using Capstone.HPTY.ServiceLayer.Interfaces.Notification;
using Capstone.HPTY.ServiceLayer.Interfaces.OrderService;
using Capstone.HPTY.ServiceLayer.Interfaces.ReplacementService;
using Capstone.HPTY.ServiceLayer.Interfaces.ScheduleService;
using Capstone.HPTY.ServiceLayer.Interfaces.ShippingService;
using Capstone.HPTY.ServiceLayer.Interfaces.StaffService;
using Capstone.HPTY.ServiceLayer.Interfaces.UserService;
using Capstone.HPTY.ServiceLayer.Services;
using Capstone.HPTY.ServiceLayer.Services.BackgroundServices;
using Capstone.HPTY.ServiceLayer.Services.ChatService;
using Capstone.HPTY.ServiceLayer.Services.Customer;
using Capstone.HPTY.ServiceLayer.Services.FeedbackService;
using Capstone.HPTY.ServiceLayer.Services.HotpotService;
using Capstone.HPTY.ServiceLayer.Services.IngredientService;
using Capstone.HPTY.ServiceLayer.Services.MailService;
using Capstone.HPTY.ServiceLayer.Services.ManagerService;
using Capstone.HPTY.ServiceLayer.Services.OrderService;
using Capstone.HPTY.ServiceLayer.Services.ReplacementService;
using Capstone.HPTY.ServiceLayer.Services.ScheduleService;
using Capstone.HPTY.ServiceLayer.Services.ShippingService;
using Capstone.HPTY.ServiceLayer.Services.StaffService;
using Capstone.HPTY.ServiceLayer.Services.UserService;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Net.payOS;

namespace Capstone.HPTY.API.AppStarts
{
    public static class DependencyInjectionContainers
    {
        public static void InstallService(this IServiceCollection services, IConfiguration configuration)
        {
            // Routing
            services.AddRouting(options =>
            {
                options.LowercaseUrls = true;
                options.LowercaseQueryStrings = true;
            });

            // Database
            services.AddDbContext<HPTYContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("Local");
                //var connectionString = configuration.GetConnectionString("Server");
                //var connectionString = configuration.GetConnectionString("DefaultConnection");
                options.UseSqlServer(connectionString,
                    sqlOptions => sqlOptions.EnableRetryOnFailure());
            });

            PayOS payOS = new PayOS(configuration["Environment:PAYOS_CLIENT_ID"] ?? throw new Exception("Cannot find environment"),
                    configuration["Environment:PAYOS_API_KEY"] ?? throw new Exception("Cannot find environment"),
                    configuration["Environment:PAYOS_CHECKSUM_KEY"] ?? throw new Exception("Cannot find environment"));

            services.AddSingleton(payOS);

            //services.AddHostedService<CartCleanupService>();

            // Core Services
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddHttpContextAccessor();

            // Auth Services
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IAuthService, AuthService>();

            // Identity Services
            services.AddScoped<ICurrentUserService, CurrentUserService>();

            // Business Services
            services.AddScoped<IComboService, ComboService>();
            services.AddScoped<IDamageDeviceService, DamageDeviceService>();
            services.AddScoped<ICustomizationService, CustomizationService>();
            services.AddScoped<IDiscountService, DiscountService>();
            services.AddScoped<IHotpotService, HotpotService>();

            services.AddScoped<IIngredientService, IngredientService>();
            services.AddScoped<ICustomizationService, CustomizationService>();
            services.AddScoped<ISizeDiscountService, SizeDiscountService>();
            services.AddScoped<IIngredientReportService, IngredientReportService>();

            services.AddScoped<IOrderService, OrderService>();

            services.AddScoped<IUnifiedProductService, UnifiedProductService>();
            services.AddScoped<IAnalyticsService, AnalyticsService>();

            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IUtensilService, UtensilService>();
            services.AddScoped<IPaymentService, PaymentService>();

            // Fix for CS0311: Ensure that InMemoryLockService implements ILockService
            // If InMemoryLockService does not implement ILockService, the following code will fail.
            // Verify the implementation of InMemoryLockService and ensure it adheres to the ILockService interface.

            services.AddSingleton<ILockService, InMemoryLockService>();

            // Manager Services
            services.AddScoped<IOrderManagementService, OrderManagementService>();
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IChatSignalRService, ChatSignalRService>();

            services.AddScoped<IFeedbackService, FeedbackService>();
            services.AddScoped<IScheduleService, ScheduleService>();
            services.AddScoped<IEquipmentConditionService, EquipmentConditionService>();
            services.AddScoped<IEquipmentStockService, EquipmentStockService>();
            services.AddScoped<IReplacementRequestService, ReplacementRequestService>();
            services.AddScoped<IManagerService, ManagerService>();

            // Staff Services
            services.AddScoped<IStaffService, StaffService>();
            services.AddScoped<IStaffPaymentService, StaffPaymentService>();
            services.AddScoped<IStaffOrderService, StaffOrderService>();
            services.AddScoped<IStaffAssignmentService, StaffAssignmentService>();


            // Notification Services
            services.AddScoped<INotificationService, NotificationService>();

            // Background Services
            //services.AddHostedService<EquipmentStockMonitorService>();



            services.Configure<IngredientMonitorOptions>(options =>
            {
                options.CheckIntervalMinutes = 60;
                options.ExpirationWarningDays = 2;
                options.AdminRole = "Admin";
                options.NotificationCooldownHours = 24;
            });

            // background hosting service
            services.AddSingleton<IHostedService, IngredientMonitorService>();
            services.AddHostedService<OrderReturnReminderService>();
            services.AddHostedService<CheckPaymentService>();

            // Shipping Services
            //services.AddScoped<IStaffShippingService, StaffShippingService>();

            // Equipment Services
            services.AddScoped<IStaffEquipmentService, StaffEquipmentService>();

            // Oder History Services
            services.AddScoped<ICustomerOrderHistoryService, CustomerOrderHistoryService>();
            services.AddScoped<IOrderHistoryService, OrderHistoryService>();

            // Rent Order Services
            services.AddScoped<IRentOrderService, RentOrderService>();
            services.AddScoped<IEquipmentReturnService, EquipmentReturnService>();

            // Email Service
            services.AddTransient<IEmailSender, EmailService>();
            services.AddScoped<EmailService>();

            // Vehicle Services
            services.AddScoped<IVehicleManagementService, VehicleManagementService>();


            services.AddSingleton<IEventPublisher, EventPublisher>();
            services.AddSingleton<IConnectionManager, ConnectionManager>();
            //services.AddSingleton<SocketIOClientService>();
            //services.AddScoped<OrderNotificationHandler>();
            //services.AddScoped<FeedbackNotificationHandler>();
            //services.AddScoped<ReplacementRequestNotificationHandler>();

            // External Services
            services.AddHttpClient();
            services.AddMemoryCache();
            services.AddHostedService<DatabaseInitializer>();

            //// AutoMapper
            //services.AddAutoMapper(typeof(MappingProfile));

            //// Validators
            //services.AddValidatorsFromAssemblyContaining<Program>();


        }
    }
}
