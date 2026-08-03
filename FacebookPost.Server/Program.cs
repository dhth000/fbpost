
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
                            "0 2 22 * * ?",
                            x => x.InTimeZone(TimeHelper.TimeZone));
                });
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

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.MapFallbackToFile("/index.html");

            app.Run();
        }
    }
}
