using ReportScheduler;
using ReportScheduler.Jobs.ReportSchedulerJob;
using ReportScheduler.Jobs.ReportSchedulerJob.Models;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();


// Register the ReportSchedulerJob as a singleton so it can be injected into Worker
builder.Services.AddSingleton<ReportSchedulerJob>();

builder.Services.Configure<EmailConfiguration>(
    builder.Configuration.GetSection("EmailConfiguration"));

var host = builder.Build();
host.Run();
