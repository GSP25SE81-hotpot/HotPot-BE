﻿using Capstone.HPTY.API.SideServices;
using Capstone.HPTY.RepositoryLayer;
using Capstone.HPTY.RepositoryLayer.Repositories;
using Capstone.HPTY.RepositoryLayer.UnitOfWork;
using Capstone.HPTY.ServiceLayer.Extensions;
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
using Capstone.HPTY.ServiceLayer.Services.ChatService;
using Capstone.HPTY.ServiceLayer.Services.ComboService;
using Capstone.HPTY.ServiceLayer.Services.Customer;
using Capstone.HPTY.ServiceLayer.Services.FeedbackService;
using Capstone.HPTY.ServiceLayer.Services.HotpotService;
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
                //var connectionString = configuration.GetConnectionString("Server");
                var connectionString = configuration.GetConnectionString("Server");
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

            // Auth Services
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IAuthService, AuthService>();

            // Business Services
            services.AddScoped<IComboService, ComboService>();
            services.AddScoped<IDamageDeviceService, DamageDeviceService>();
            services.AddScoped<ICustomizationService, CustomizationService>();
            services.AddScoped<IDiscountService, DiscountService>();
            services.AddScoped<IHotpotService, HotpotService>();

            services.AddScoped<IIngredientService, IngredientService>();
            services.AddScoped<ICustomizationService, CustomizationService>();
            services.AddScoped<ISizeDiscountService, SizeDiscountService>();

            services.AddScoped<IOrderService, OrderService>();

            services.AddScoped<IUnifiedProductService, UnifiedProductService>();
            services.AddScoped<IAnalyticsService, AnalyticsService>();

            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IUtensilService, UtensilService>();
            services.AddScoped<IPaymentService, PaymentService>();

            // Manager Services
            services.AddScoped<IOrderManagementService, OrderManagementService>();
            services.AddScoped<IChatService, ChatService>();
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

            // Notification Services
            services.AddScoped<INotificationService, SignalRNotificationService>();

            // Background Services
            services.AddHostedService<EquipmentStockMonitorService>();

            // Shipping Services
            services.AddScoped<IStaffShippingService, StaffShippingService>();

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


            services.AddSingleton<IEventPublisher, EventPublisher>();
            services.AddSingleton<IConnectionManager, ConnectionManager>();

            // External Services
            services.AddHttpClient();
            services.AddMemoryCache();

            //// AutoMapper
            //services.AddAutoMapper(typeof(MappingProfile));

            //// Validators
            //services.AddValidatorsFromAssemblyContaining<Program>();


        }
    }
}
