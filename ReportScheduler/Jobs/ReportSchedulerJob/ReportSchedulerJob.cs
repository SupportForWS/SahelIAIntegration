
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ReportScheduler.Jobs.ReportSchedulerJob.DTOs;
using ReportScheduler.Jobs.ReportSchedulerJob.Models;
using ReportScheduler.Models;
using ReportScheduler.Services;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;

namespace ReportScheduler.Jobs.ReportSchedulerJob
{



    public class ReportSchedulerJob
    {
        private readonly string _connectionString;
        private readonly ILogger<ReportSchedulerJob> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly ReportSettings _reportSettings;
        private readonly EmailConfiguration _emailConfig;
        private readonly ApiAuthenticationService _authService;
        private readonly IHttpClientFactory _httpClientFactory;

        public ReportSchedulerJob(
            IConfiguration configuration,
            ILogger<ReportSchedulerJob> logger,
            IOptions<ApiSettings> apiSettings,
            IOptions<ReportSettings> reportSettings,
            IOptions<EmailConfiguration> emailOptions,
            ApiAuthenticationService authService,
            IHttpClientFactory httpClientFactory)
        {
            _connectionString = configuration.GetConnectionString("Default");
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _reportSettings = reportSettings.Value;
            _emailConfig = emailOptions.Value;
            _authService = authService;
            _httpClientFactory = httpClientFactory;
        }

        public async Task Run()
        {
            var reports = await GetReportsToRunTodayAsync();

            foreach (var report in reports)
            {
                try
                {
                    var reportResult = await DirectExecuteReportAsync(report);

                    if (reportResult?.DownloadFileDTO != null)
                    {
                        var fullPath = await SaveReportFileAsync(reportResult.DownloadFileDTO);
                        await UpdateReportFilePathDatabaseAsync(report.ReportId, fullPath);

                        await SendReportLinksToSharesAsync(report.ReportId);

                        await UpdateReportExecutionTimeAsync(report.Id);

                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process scheduled report with Id {Id}", report.Id);
                }
            }
        }

        private async Task<List<ReportShareDTO>> GetPendingShares(int reportParameterId)
        {
            var shares = new List<ReportShareDTO>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
            SELECT Id, ReportParameterId, Token, Email, ExpiresAt, CreatedAt, AccessedAt, IsRevoked
            FROM ReportShares 
            WHERE ReportParameterId = @ReportParameterId 
              AND IsRevoked = 0 
              AND ExpiresAt > GETDATE()";

            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@ReportParameterId", reportParameterId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                shares.Add(new ReportShareDTO
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    ReportParameterId = reader.GetInt32(reader.GetOrdinal("ReportParameterId")),
                    Token = reader.GetString(reader.GetOrdinal("Token")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    ExpiresAt = reader.GetDateTime(reader.GetOrdinal("ExpiresAt")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    AccessedAt = reader.IsDBNull(reader.GetOrdinal("AccessedAt"))
                        ? (DateTime?)null
                        : reader.GetDateTime(reader.GetOrdinal("AccessedAt")),
                    IsRevoked = reader.GetBoolean(reader.GetOrdinal("IsRevoked"))
                });
            }

            return shares;
        }

        private async Task<bool> UpdateReportFilePathDatabaseAsync(int reportId, string fullPath)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
            UPDATE GeneralStatisticalReportParameters 
            SET FilePath = @FilePath, 
                LastGeneratedAt = @LastGeneratedAt  
            WHERE Ser = @Id";

            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@FilePath", fullPath);
            cmd.Parameters.AddWithValue("@LastGeneratedAt", DateTime.Now);
            cmd.Parameters.AddWithValue("@Id", reportId);

            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }

        private async Task<List<ReportExecutionScheduleModel>> GetReportsToRunTodayAsync()
        {
            var reports = new List<ReportExecutionScheduleModel>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var cmd = new SqlCommand("GetScheduledReportsToRunToday", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

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
                    SharedWith = reader.GetString(reader.GetOrdinal("SharedWith"))
                });
            }
             return reports;
        }

        private async Task<ResponseDto> DirectExecuteReportAsync(ReportExecutionScheduleModel model)
        {
            var url = $"{_apiSettings.BaseUrl}{_apiSettings.DirectExecuteReportPath}";

            return await CallAuthenticatedPostAPI<ExecuteReportModel, ResponseDto>(
                new ExecuteReportModel
                {
                    IsSampleData = true,
                    ReportID = model.ReportId,
                    AutoUpdateDates=true
                },
                url);
        }


        private async Task<string> SaveReportFileAsync(DownloadFileDTO fileDto)
        {
            if (string.IsNullOrWhiteSpace(fileDto.File))
            {
                _logger.LogWarning("Empty file content for report file {FileName}", fileDto.FileName);
                return string.Empty;
            }

            try
            {
                var fileBytes = Convert.FromBase64String(fileDto.File);

                // Ensure directory exists on remote server
                if (!Directory.Exists(_reportSettings.FolderPath))
                {
                    Directory.CreateDirectory(_reportSettings.FolderPath);
                    _logger.LogInformation("Created directory {FolderPath}", _reportSettings.FolderPath);
                }

                var extension = fileDto.FileExtension?.StartsWith('.') == true
                    ? fileDto.FileExtension
                    : "." + fileDto.FileExtension;

                // Add timestamp to avoid file name conflicts
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileDto.FileName);
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var fileName = $"{fileNameWithoutExt}_{timestamp}{extension}";

                var fullPath = Path.Combine(_reportSettings.FolderPath, fileName);

                await File.WriteAllBytesAsync(fullPath, fileBytes);

                _logger.LogInformation("Saved report file to {FilePath}", fullPath);
                return fullPath;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied saving file to {FolderPath}", _reportSettings.FolderPath);
                throw new Exception("Access denied to file server. Check service account permissions.");
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error saving file to {FolderPath}", _reportSettings.FolderPath);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving report file to {FolderPath}", _reportSettings.FolderPath);
                throw;
            }
        }

        private async Task UpdateReportExecutionTimeAsync(int reportScheduleId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
            UPDATE ReportExecutionSchedule 
            SET LastExecutionDateTime = @Now 
            WHERE Id = @Id";

            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Now", DateTime.Now);
            cmd.Parameters.AddWithValue("@Id", reportScheduleId);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<TResponse> CallAuthenticatedPostAPI<TRequest, TResponse>(TRequest requestDto, string apiUrl)
        {
            int retryCount = 0;

            while (retryCount < _reportSettings.RetryCount)
            {
                try
                {
                    _logger.LogInformation("Calling API {ApiUrl} (Attempt {Attempt}/{Max})",
                        apiUrl, retryCount + 1, _reportSettings.RetryCount);

                    var token = await _authService.GetValidTokenAsync();
                    using var httpClient = _httpClientFactory.CreateClient();

                    var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Content = JsonContent.Create(requestDto);

                    var response = await httpClient.SendAsync(request);

                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<ResponseWrapper<TResponse>>(responseContent);

                    if (!response.IsSuccessStatusCode || !result.Succeeded)
                    {
                        _logger.LogError("API call failed: {StatusCode} - {Error}", response.StatusCode, responseContent);

                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && retryCount < _reportSettings.RetryCount - 1)
                        {
                            _logger.LogWarning("Unauthorized, retrying with new token...");
                            retryCount++;
                            await Task.Delay(1000 * retryCount);
                            continue;
                        }

                        throw new HttpRequestException($"API returned {result.Message}");
                    }


                    return result.Data;
                }
                catch (Exception ex) when (retryCount < _reportSettings.RetryCount - 1)
                {
                    retryCount++;
                    _logger.LogWarning(ex, "API call failed, retrying {Retry}/{Max}...", retryCount, _reportSettings.RetryCount);
                    await Task.Delay(2000 * retryCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "API call failed to {ApiUrl} after {RetryCount} attempts", apiUrl, _reportSettings.RetryCount);
                    throw;
                }
            }

            return default;
        }

        private async Task<TResponse> CallAuthenticatedGetAPI<TResponse>(string apiUrl)
        {
            try
            {
                _logger.LogInformation("Calling GET API {ApiUrl}", apiUrl);

                var token = await _authService.GetValidTokenAsync();
                using var httpClient = _httpClientFactory.CreateClient();

                var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("GET API call failed: {StatusCode} - {Error}", response.StatusCode, error);
                    throw new HttpRequestException($"API returned {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ResponseWrapper<TResponse>>(responseContent);

                return result.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET API call failed to {ApiUrl}", apiUrl);
                throw;
            }
        }


        private async Task SendReportLinkByEmailAsync(string reportLink, string toEmail, DateTime expiresAt, string reportName = "Statistical Report")
        {
            if (!_emailConfig.EnableSendingMail)
            {
                _logger.LogWarning("Email sending is disabled in configuration.");
                return;
            }

            int retryCount = 0;
            int maxRetries = _reportSettings.RetryCount;

            while (retryCount < maxRetries)
            {
                try
                {
                    _logger.LogInformation("Sending email to {Email} (Attempt {Attempt}/{Max})",
                        toEmail, retryCount + 1, maxRetries);

                    using var message = new MailMessage
                    {
                        From = new MailAddress(_emailConfig.FromEmail),
                        Subject = $"Your {reportName} is Ready",
                        Body = BuildEmailBody(reportLink, expiresAt, reportName),
                        IsBodyHtml = true
                    };

                    message.To.Add(toEmail.Trim());

                    using var smtp = new SmtpClient(_emailConfig.ExchangeServer)
                    {
                        Port = _emailConfig.Port,
                        Credentials = new NetworkCredential(
                            _emailConfig.UserName,
                            _emailConfig.Password,
                            _emailConfig.DomainName
                        ),
                        EnableSsl = false
                    };

                    await smtp.SendMailAsync(message);
                    _logger.LogInformation("Report link sent successfully to {Email}", toEmail);
                    return; // Success, exit method
                }
                catch (SmtpException ex) when (retryCount < maxRetries - 1)
                {
                    retryCount++;
                    _logger.LogWarning(ex, "SMTP error sending to {Email}, retrying {Retry}/{Max}...",
                        toEmail, retryCount, maxRetries);
                    await Task.Delay(2000 * retryCount); // Exponential backoff
                }
                catch (Exception ex) when (retryCount < maxRetries - 1)
                {
                    retryCount++;
                    _logger.LogWarning(ex, "Error sending to {Email}, retrying {Retry}/{Max}...",
                        toEmail, retryCount, maxRetries);
                    await Task.Delay(2000 * retryCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send email to {Email} after {RetryCount} attempts",
                        toEmail, maxRetries);
                    throw;
                }
            }
        }

        public async Task SendReportLinksToSharesAsync(int reportParameterId)
        {
            var shares = await GetPendingShares(reportParameterId);

            if (!shares.Any())
            {
                _logger.LogWarning("No pending shares found for report {ReportParameterId}", reportParameterId);
                return;
            }

            var baseUrl = _apiSettings.BaseUrl;
            var accessPath = _apiSettings.AccessReportPath.Replace("{token}", "");

            int successCount = 0;
            int failureCount = 0;

            foreach (var share in shares)
            {
                //update to send UI links
              //  var reportLink = $"{baseUrl}{accessPath}{share.Token}";
                var reportLink = string.Format(_apiSettings.UILinksWithToken, share.Token);
                try
                {
                    await SendReportLinkByEmailAsync(reportLink, share.Email, share.ExpiresAt);
                    successCount++;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogError(ex, "Failed to send link to {Email} for report {ReportParameterId} after all retry attempts",
                        share.Email, reportParameterId);
                    // Continue to next recipient instead of failing entire batch
                }
            }

            _logger.LogInformation(
                "Email sending completed for report {ReportParameterId}: {SuccessCount} succeeded, {FailureCount} failed out of {TotalCount} total",
                reportParameterId, successCount, failureCount, shares.Count);
        }

        private static string BuildEmailBody(string reportLink, DateTime expiresAt, string reportName)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .button {{ 
            display: inline-block; 
            padding: 12px 24px; 
            background-color: #007bff; 
            color: #ffffff !important; 
            text-decoration: none; 
            border-radius: 4px;
            margin: 20px 0;
        }}
        .footer {{ color: #666; font-size: 12px; margin-top: 30px; }}
        .warning {{ color: #856404; background-color: #fff3cd; padding: 10px; border-radius: 4px; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <h2>Your {reportName} is Ready</h2>
        <p>Hello,</p>
        <p>Your requested report has been generated and is ready to view.</p>
        
        <a href='{reportLink}' class='button'>View Report</a>
        
        <div class='warning'>
            <strong>Important:</strong>
            <ul>
                <li>This link will expire on <strong>{expiresAt:dddd, MMMM dd, yyyy 'at' hh:mm tt}</strong></li>
                <li>The link is personalized and tracked for security purposes</li>
                <li>Do not share this link with others</li>
            </ul>
        </div>
        
        <p>If you did not request this report, please contact support.</p>
        
        <div class='footer'>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
        }
    }




}
