using Azure;
using Microsoft.Extensions.Options;
using ReportScheduler.DTOs;
using ReportScheduler.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace ReportScheduler.Services
{
    public class ApiAuthenticationService
    {

        private readonly HttpClient _httpClient;
        private readonly ApiSettings _apiSettings;
        private readonly ServiceAccountSettings _serviceAccount;
        private readonly ILogger<ApiAuthenticationService> _logger;
        private string _jwtToken;
        private string _refreshToken;
        private DateTime _tokenExpiry;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public ApiAuthenticationService(
            IHttpClientFactory httpClientFactory,
            IOptions<ApiSettings> apiSettings,
            IOptions<ServiceAccountSettings> serviceAccount,
            ILogger<ApiAuthenticationService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiSettings = apiSettings.Value;
            _serviceAccount = serviceAccount.Value;
            _logger = logger;
        }

        public async Task<string> GetValidTokenAsync()
        {
            if (!string.IsNullOrEmpty(_jwtToken) && _tokenExpiry > DateTime.UtcNow.AddMinutes(5))
            {
                return _jwtToken;
            }

            await _semaphore.WaitAsync();
            try
            {
                if (!string.IsNullOrEmpty(_jwtToken) && _tokenExpiry > DateTime.UtcNow.AddMinutes(5))
                {
                    return _jwtToken;
                }

                if (!string.IsNullOrEmpty(_refreshToken))
                {
                    try
                    {
                        await RefreshTokenAsync();
                        return _jwtToken;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Token refresh failed, attempting full authentication");
                    }
                }

                await AuthenticateAsync();
                return _jwtToken;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task AuthenticateAsync()
        {
            try
            {
                var loginRequest = new LoginModel
                {
                    UserId = _serviceAccount.UserId,
                    HandshakeKey = _serviceAccount.HandshakeKey
                };

                _logger.LogInformation("Authenticating service account: {UserId}", loginRequest.UserId);

                var url = $"{_apiSettings.BaseUrl}{_apiSettings.ExternalLoginPath}";
                var response = await _httpClient.PostAsJsonAsync(url, loginRequest);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Authentication failed: {StatusCode} - {Error}", response.StatusCode, error);
                    throw new UnauthorizedAccessException($"Authentication failed: {response.StatusCode}");
                }

                var result = await response.Content.ReadFromJsonAsync<DTOs.Response<TokenResponse>>();

                if (result?.Data == null)
                {
                    throw new UnauthorizedAccessException("Authentication returned null response");
                }

                _jwtToken = result.Data.AccessToken;
                _refreshToken = result.Data.RefreshToken;
                _tokenExpiry = DateTime.UtcNow.AddMinutes(result.Data.ExpiresIn);

                _logger.LogInformation("Service authenticated successfully. Token expires at {ExpiresAt}", _tokenExpiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to authenticate service account");
                throw;
            }
        }

        private async Task RefreshTokenAsync()
        {
            try
            {
                var refreshRequest = new { RefreshToken = _refreshToken };
                var url = $"{_apiSettings.BaseUrl}{_apiSettings.RefreshTokenPath}";

                var response = await _httpClient.PostAsJsonAsync(url, refreshRequest);

                if (!response.IsSuccessStatusCode)
                {
                    throw new UnauthorizedAccessException("Token refresh failed");
                }

                var result = await response.Content.ReadFromJsonAsync<DTOs.Response<TokenResponse>>();

                _jwtToken = result.Data.AccessToken;
                _refreshToken = result.Data.RefreshToken;
                _tokenExpiry = DateTime.UtcNow.AddMinutes(result.Data.ExpiresIn);

                _logger.LogInformation("Token refreshed successfully. New expiry: {ExpiresAt}", _tokenExpiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh token");
                throw;
            }
        }
    }
}
