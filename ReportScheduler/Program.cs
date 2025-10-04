using ReportScheduler;
using ReportScheduler.Jobs.ReportSchedulerJob;
using ReportScheduler.Jobs.ReportSchedulerJob.Models;
using ReportScheduler.Models;
using ReportScheduler.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

// Register HttpClient factory
builder.Services.AddHttpClient();




builder.Services.Configure<EmailConfiguration>(
    builder.Configuration.GetSection("EmailConfiguration"));


builder.Services.Configure<ServiceAccountSettings>(
    builder.Configuration.GetSection("ServiceAccount"));



builder.Services.Configure<ReportSettings>(
    builder.Configuration.GetSection("ReportSettings"));

builder.Services.Configure<ApiSettings>(
    builder.Configuration.GetSection("ApiSettings"));




builder.Services.AddSingleton<ApiAuthenticationService>();

// Register the ReportSchedulerJob as a singleton so it can be injected into Worker
builder.Services.AddSingleton<ReportSchedulerJob>();


var host = builder.Build();
host.Run();
