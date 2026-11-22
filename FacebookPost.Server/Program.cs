
using FacebookPost.Server.Builder;
using FacebookPost.Server.Helper;
using FacebookPost.Server.Model;
using FacebookPost.Server.Providers;
using FacebookPost.Server.SchedulerJobs;
using FacebookPost.Server.Service;
using Quartz;

namespace FacebookPost.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddHttpClient<IShopeeService, ShopeeService>();
            builder.Services.AddSingleton<ITemplateService, TemplateService>();
            builder.Services.AddScoped<IFacebookPostBuilder, FacebookPostBuilder>();
            builder.Services.AddScoped<IFacebookService, FacebookService>();
            builder.Services.AddScoped<IProductDataService, ProductDataService>();
            builder.Services.AddScoped<IShopeeLinkService, ShopeeLinkService>();
            builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

            builder.Services.AddQuartz(q =>
            {
                var jobKey = new JobKey("PublishAllFacebook");

                q.AddJob<PublishAllFacebookJob>(options =>
                {
                    options.WithIdentity(jobKey);
                });

                q.AddTrigger(options =>
                {
                    options
                        .ForJob(jobKey)
                        .WithIdentity("PublishAllFacebookTrigger")
                        .WithCronSchedule(
                            builder.Configuration["Quartz:PublishAllFacebookCron"]
                                ?? throw new InvalidOperationException("Missing config Quartz:PublishAllFacebookCron"),
                            x => x.InTimeZone(TimeHelper.TimeZone));
                });

                // Job test: ghi log mỗi phút (logs/heartbeat.log) để kiểm tra Quartz có chạy trên IIS không.
                // Bỏ comment cả AddJob và AddTrigger khi cần test (job không có trigger sẽ làm Quartz báo lỗi).
                //var heartbeatJobKey = new JobKey("Heartbeat");
                //
                //q.AddJob<HeartbeatJob>(options => options.WithIdentity(heartbeatJobKey));
                //
                //q.AddTrigger(options =>
                //{
                //    options
                //        .ForJob(heartbeatJobKey)
                //        .WithIdentity("HeartbeatTrigger")
                //        .StartNow()
                //        .WithCronSchedule(
                //            builder.Configuration["Quartz:HeartbeatCron"] ?? "0 * * ? * *",
                //            x => x.InTimeZone(TimeHelper.TimeZone));
                //});
            });

            builder.Services.AddQuartzHostedService(options =>
            {
                options.WaitForJobsToComplete = true;
            });

            var app = builder.Build();

            app.UseDefaultFiles();
            app.MapStaticAssets();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.MapGet("/health", () => Results.Ok("OK"));

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.MapFallbackToFile("/index.html");

            app.Run();
        }
    }
}
