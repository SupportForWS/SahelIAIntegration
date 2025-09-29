
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ReportScheduler.Jobs.ReportSchedulerJob.DTOs;
using ReportScheduler.Jobs.ReportSchedulerJob.Models;
using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;

namespace ReportScheduler.Jobs.ReportSchedulerJob
{
    public class ReportSchedulerJob
    {
        private readonly string _connectionString;
        private readonly ILogger<ReportSchedulerJob> _logger;
        private readonly string _reportDetailsApiUrl;
        private readonly string _executeReportApiUrl;
        private readonly string _directExecuteReportApiUrl;
        private readonly string _reportsFolderPath;
        private readonly EmailConfiguration _emailConfig;

        public ReportSchedulerJob(IConfiguration configuration,
                                  ILogger<ReportSchedulerJob> logger,
                                  IOptions<EmailConfiguration> emailOptions)
        {
            _connectionString = configuration.GetConnectionString("Default");
            _logger = logger;
            _reportDetailsApiUrl = configuration["ApiUrls:ReportDetails"];
            _executeReportApiUrl = configuration["ApiUrls:ExecuteReport"];
            _reportsFolderPath = configuration["ReportsFolderPath"];
            _emailConfig = emailOptions.Value;
            _directExecuteReportApiUrl = configuration["ApiUrls:DirectExecuteReportApiUrl"];
        }

        public async Task Run()
        {
            var reports = await GetReportsToRunTodayAsync();

            foreach (var report in reports)
            {
                try
                {
                    //var reportDetails = await GetReportDetailsAsync(report.ParameterHistoryId);
                    //if (reportDetails == null || reportDetails.Serial == null)
                    //{
                    //    _logger.LogWarning("Report details not found for ReportId {ReportId}", report.ReportId);
                    //    continue;
                    //}

                    //reportDetails.Serial = 0; // report.ReportId; //todo here serial is the start index of data
                    //var reportResult = await ExecuteReportAsync(reportDetails);
                    var reportResult = await DirectExecuteReportAsync(report);

                    if (reportResult?.DownloadFileDTO != null)
                    {
                        var fullPath = await SaveReportFileAsync(reportResult.DownloadFileDTO);

                        //todo add retry count
                        //todo what if email is null
                        await SendReportFileByEmailAsync(fullPath, report.SharedWith, "Report File", "Please find the attached report.");
                    }

                    await UpdateReportExecutionTimeAsync(report.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process scheduled report with Id {Id}", report.Id);
                }
            }
        }

        private async Task<List<ReportExecutionScheduleModel>> GetReportsToRunTodayAsync()
        {

            //var reports1 = new List<ReportExecutionScheduleModel>
            //    {
            //        new ReportExecutionScheduleModel
            //        {
            //            Id = 2,
            //            ReportId = 11125,
            //            ScheduleType = "O",
            //            ExecutionTime = DateTime.Today.AddHours(9),
            //            Enabled = true,
            //            ParameterHistoryId=622,
            //            SharedWith="www@fff.com,www@fff.com"
            //        },
            //        //new ReportExecutionScheduleModel
            //        //{
            //        //    Id = 2,
            //        //    ReportId = 102,
            //        //    ScheduleType = "W",
            //        //    ExecutionTime = DateTime.Today.AddHours(14),
            //        //    Enabled = true
            //        //},
            //        //new ReportExecutionScheduleModel
            //        //{
            //        //    Id = 3,
            //        //    ReportId = 103,
            //        //    ScheduleType = "O",
            //        //    ExecutionTime = DateTime.Today.AddHours(16),
            //        //    Enabled = false
            //        //}
            //    };

            //return reports1;

            ///
            var reports = new List<ReportExecutionScheduleModel>();

            await using var connection = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand("GetScheduledReportsToRunToday", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            await connection.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                reports.Add(new ReportExecutionScheduleModel
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    ReportId = reader.GetInt32(reader.GetOrdinal("ReportId")),
                    ScheduleType = reader.GetString(reader.GetOrdinal("ScheduleType")),
                    ExecutionTime = reader.GetDateTime(reader.GetOrdinal("ExecutionTime")),
                    Enabled = reader.GetBoolean(reader.GetOrdinal("Enabled")),
                  //  ParameterHistoryId = reader.GetInt32(reader.GetOrdinal("ParameterHistoryId")),
                    SharedWith = reader.GetString(reader.GetOrdinal("SharedWith"))
                });
            }

            return reports;
        }

        private async Task<SaveCriteriaDTO> GetReportDetailsAsync(int reportId)
        {
            var result = await CallGetAPI<SaveCriteriaDTO>(string.Format(_reportDetailsApiUrl, reportId));

            return result;
        }

        private async Task<ResponseDto> DirectExecuteReportAsync(ReportExecutionScheduleModel model)
        {
            //todo check qasem
            //todo set these from db
            //saveCriteria.DeclarationChoice = "Normal";
            //saveCriteria.IsSampleData = true;
            return await CallPostAPI<ExecuteReportModel, ResponseDto>(new ExecuteReportModel
            {
                IsSampleData = true,
                ReportID=model.ReportId
            }, _directExecuteReportApiUrl);

        }

        private async Task<ResponseDto> ExecuteReportAsync(SaveCriteriaDTO saveCriteria)
        {
            //todo check qasem
            //todo set these from db
            saveCriteria.DeclarationChoice = "Normal";
            saveCriteria.IsSampleData = true;
            return await CallPostAPI<SaveCriteriaDTO, ResponseDto>(saveCriteria, _executeReportApiUrl);
        }


        //todo: try send the file without saving it
        private async Task<string> SaveReportFileAsync(DownloadFileDTO fileDto)
        {
            if (string.IsNullOrWhiteSpace(fileDto.File))
            {
                _logger.LogWarning("Empty file content for report file {FileName}", fileDto.FileName);
                return string.Empty;
            }

            var fileBytes = Convert.FromBase64String(fileDto.File);

            Directory.CreateDirectory(_reportsFolderPath);

            var extension = fileDto.FileExtension?.StartsWith('.') == true ? fileDto.FileExtension : "." + fileDto.FileExtension;
            var fileName = fileDto.FileName.EndsWith(extension) ? fileDto.FileName : fileDto.FileName + extension;

            var fullPath = Path.Combine(_reportsFolderPath, fileName);

            await File.WriteAllBytesAsync(fullPath, fileBytes);

            _logger.LogInformation("Saved report file to {FilePath}", fullPath);

            return fullPath;
        }

        private async Task UpdateReportExecutionTimeAsync(int reportScheduleId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var cmd = new SqlCommand(
                "UPDATE ReportExecutionSchedule SET LastExecutionDateTime = @Now WHERE Id = @Id", connection);
            cmd.Parameters.AddWithValue("@Now", DateTime.Now);
            cmd.Parameters.AddWithValue("@Id", reportScheduleId);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<TResponse> CallPostAPI<TRequest, TResponse>(TRequest requestDto, string apiUrl)
        {
            try
            {
                _logger.LogInformation("Calling API {ApiUrl} with DTO {@RequestDto}", apiUrl, requestDto);



                using var httpClient = new HttpClient();
                var json = JsonConvert.SerializeObject(requestDto);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(apiUrl, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("API response: {ResponseContent}", responseContent);


                var result = JsonConvert.DeserializeObject<ResponseWrapper<TResponse>>(responseContent);
                return result.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API call failed to {ApiUrl}", apiUrl);
                return default;
            }
        }

        private async Task<TResponse> CallGetAPI<TResponse>(string apiUrl)
        {
            try
            {
                _logger.LogInformation("Calling GET API {ApiUrl}", apiUrl);

                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("API response: {ResponseContent}", responseContent);

                var result = JsonConvert.DeserializeObject<ResponseWrapper<TResponse>>(responseContent);
                return result.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET API call failed to {ApiUrl}", apiUrl);
                return default;
            }
        }

        private async Task SendReportFileByEmailAsync(string filePath, string toEmails, string subject, string body)
        {
            if (!_emailConfig.EnableSendingMail)
            {
                _logger.LogWarning("Email sending is disabled in configuration.");
                return;
            }

            try
            {
                //add certificated pass code todo
                using var message = new MailMessage
                {
                    From = new MailAddress(_emailConfig.FromEmail),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                foreach (var email in toEmails.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    message.To.Add(email.Trim());
                }

                message.Attachments.Add(new Attachment(filePath));

                using var smtp = new SmtpClient(_emailConfig.ExchangeServer)
                {
                    Port = _emailConfig.Port,
                    Credentials = new NetworkCredential(
                        _emailConfig.UserName,
                        _emailConfig.Password,
                        _emailConfig.DomainName
                    ),
                    EnableSsl = false // Exchange on port 25 typically doesn't use SSL
                };

                await smtp.SendMailAsync(message);

                _logger.LogInformation("Email sent to {Recipients}", toEmails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email with file {FilePath}", filePath);
                throw;
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted temporary report file {FilePath}", filePath);
                }
            }
        }
    }



    public class ExecuteReportModel
    {
        public int ReportID { get; set; }
        public bool IsSampleData { get; set; }
    }


}
