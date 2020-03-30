using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

using BunqClientLight.BunqModels;

namespace BunqClientLight
{
    public class BunqApiSetup
    {
        private readonly HttpClient httpClient;

        public BunqApiSetup(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task<SandboxUser> GetSandboxUser()
        {
            var response = await httpClient.PostAsync("sandbox-user", null);
            var sandboxUser = await response.FromBunqJson<SandboxUser>("ApiKey");

            return sandboxUser;
        }

        public async Task<(Token token, ServerPublicKey serverPublicKey)> Installation(string publicKeyPem)
        {
            var response = await PostJsonAsync("installation", new { client_public_key = publicKeyPem });
            var data = await response.FromBunqJson<Token, ServerPublicKey>("Token", "ServerPublicKey");

            return data;
        }

        private Task<HttpResponseMessage> PostJsonAsync(string url, object data)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(data);

            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            return httpClient.PostAsync(url, content);
        }
    }

    public class BunqApi
    {
        private readonly HttpClient httpClient;
        private readonly string apiKey;
        private readonly string installationToken;
        private readonly SignSignature signSignature;
        private Session session;

        public BunqApi(HttpClient httpClient, string apiKey, string privatePem, string publicServerPem, string installationToken)
        {
            this.httpClient = httpClient;
            this.apiKey = apiKey;
            this.installationToken = installationToken;
            signSignature = new SignSignature(privatePem, publicServerPem);
        }

        public Task<Id> SetupDevice()
        {
            return PostJsonAsync<Id>(
                $"device-server",
                "Id",
                new
                {
                    secret = apiKey,
                    description = Environment.MachineName,
                    permitted_ips = Array.Empty<string>()
                },
                installationToken);
        }

        public async Task SetupSession()
        {
            var (token, userPerson, userCompany) = await PostJsonAsync<Token, UserPerson, UserCompany>(
                "session-server",
                "Token", "UserPerson", "UserCompany",
                new { secret = apiKey },
                installationToken);

            var userId = userPerson?.id ?? userCompany.id;
            var session_timeout = userPerson?.session_timeout ?? userCompany.session_timeout;

            session = new Session(userId, token.token, session_timeout);
        }

        public Task<MonetaryAccountBank[]> ListMonetaryAccountBanks()
        {
            return GetJsonArray<MonetaryAccountBank>(
                $"user/{session.UserId}/monetary-account-bank",
                "MonetaryAccountBank");
        }

        public Task<Id> RequireInquiry(Amount amountInquired, Pointer counterpartyAlias, string description, long monetaryAccountId)
        {
            return PostJsonAsync<Id>(
                $"user/{session.UserId}/monetary-account/{monetaryAccountId}/request-inquiry",
                "Id",
                new
                {
                    amount_inquired = amountInquired,
                    counterparty_alias = counterpartyAlias,
                    description = description,
                    allow_bunqme = false
                });
        }

        public Task<Payment[]> ListPayments(long monetaryAccountId)
        {
            return GetJsonArray<Payment>(
                $"user/{session.UserId}/monetary-account/{monetaryAccountId}/payment",
                "Payment");
        }

        public Task<Id> Payment(Amount amount, Pointer counterpartyAlias, string description, long monetaryAccountId)
        {
            return PostJsonAsync<Id>(
                $"user/{session.UserId}/monetary-account/{monetaryAccountId}/payment",
                "Id",
                new
                {
                    amount = amount,
                    counterparty_alias = counterpartyAlias,
                    description = description
                });
        }

        private async Task<T[]> GetJsonArray<T>(string url, string wrapperTag)
        {
            var response = await Send(HttpMethod.Get, url);
            return await response.FromBunqJsonArray<T>(wrapperTag);
        }

        private async Task<T> PostJsonAsync<T>(string url, string wrapperTag, object data, string token = null)
        {
            var response = await Send(HttpMethod.Post, url, data, token);
            return await response.FromBunqJson<T>(wrapperTag);
        }

        private async Task<(T1, T2, T3)> PostJsonAsync<T1, T2, T3>(string url, string wrapperTag1, string wrapperTag2, string wrapperTag3, object data, string token = null)
        {
            var response = await Send(HttpMethod.Post, url, data, token);
            return await response.FromBunqJson<T1, T2, T3>(wrapperTag1, wrapperTag2, wrapperTag3);
        }

        private async Task<HttpResponseMessage> Send(HttpMethod httpMethod, string url, object data = null, string token = null)
        {
            while(true)
            {
                var requestMessage = await CreateRequest(httpMethod, url, data, token);
                var responseMessage = await httpClient.SendAsync(requestMessage);

                // check for TooManyRequests
                if (responseMessage.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    await Task.Delay(GetRetryAfter(responseMessage));
                    // resend request
                    continue;
                }

                await VerifyResponse(responseMessage);

                return responseMessage;
            }

            static int GetRetryAfter(HttpResponseMessage responseMessage)
            {
                var retryAfter = 15000; // 15 seconds at default

                if (responseMessage.Headers.TryGetValues("Retry-After", out var values))
                {
                    foreach (var v in values)
                    {
                        if (int.TryParse(v, out var retrySeconds))
                        {
                            retryAfter = retrySeconds;
                            break;
                        }
                    }
                }

                return retryAfter;
            }
        }

        private async Task<HttpRequestMessage> CreateRequest(HttpMethod httpMethod, string url, object data = null, string token = null)
        {
            var requestMessage = new HttpRequestMessage(httpMethod, url);

            var bytes = data == null ? Array.Empty<byte>() : JsonSerializer.SerializeToUtf8Bytes(data);
            if (data != null)
            {
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                requestMessage.Content = content;
            }

            await SignAndSetHeaders(requestMessage.Headers, token, bytes);

            return requestMessage;
        }

        private async Task SignAndSetHeaders(HttpHeaders headers, string token, byte[] bytes)
        {
            if (token != installationToken)
            {
                // validate session
                if (session == null || !session.IsValid())
                {
                    // refresh session token
                    await SetupSession();
                }
                token = session.SessionToken;
            }

            var signature = signSignature.SignData(bytes);
            headers.Add("X-Bunq-Client-Authentication", token);
            headers.Add("X-Bunq-Client-Signature", signature);
        }

        private async Task VerifyResponse(HttpResponseMessage response)
        {
            var bodyBytes = await response.Content.ReadAsByteArrayAsync();
            var serverSignatureHeader = string.Join(",", response.Headers.GetValues("X-Bunq-Server-Signature"));

            if (!signSignature.VerifyData(bodyBytes, serverSignatureHeader))
            {
                throw new InvalidOperationException("Invalid Server-Signature");
            }
        }
    
        class Session
        {
            public long UserId { get; }
            public string SessionToken { get; }

            private readonly DateTime expiryTime;

            public Session(long userId, string sessionToken, int sessionTimeout)
            {
                UserId = userId;
                SessionToken = sessionToken;
                expiryTime = DateTime.Now.AddSeconds(sessionTimeout);
            }

            public bool IsValid() => expiryTime - DateTime.Now >= TimeSpan.FromSeconds(30);
        }
    }

    static class HttpBunqApiExtensions
    {
        public static async Task<T> FromBunqJson<T>(this HttpResponseMessage response, string wrapperTag)
        {
            var responseObject = await response.GetResponse();
            var data = responseObject.Deserialize<T>(wrapperTag);
            return data;
        }

        public static async Task<T[]> FromBunqJsonArray<T>(this HttpResponseMessage response, string wrapperTag)
        {
            var responseObject = await response.GetResponse();
            var data = responseObject.DeserializeArray<T>(wrapperTag).ToArray();
            return data;
        }

        public static async Task<(T1, T2)> FromBunqJson<T1, T2>(this HttpResponseMessage response, string wrapperTag1, string wrapperTag2)
        {
            var responseObject = await response.GetResponse();
            var data1 = responseObject.Deserialize<T1>(wrapperTag1);
            var data2 = responseObject.Deserialize<T2>(wrapperTag2);
            return (data1, data2);
        }

        public static async Task<(T1, T2, T3)> FromBunqJson<T1, T2, T3>(this HttpResponseMessage response, string wrapperTag1, string wrapperTag2, string wrapperTag3)
        {
            var responseObject = await response.GetResponse();
            var data1 = responseObject.Deserialize<T1>(wrapperTag1);
            var data2 = responseObject.Deserialize<T2>(wrapperTag2);
            var data3 = responseObject.Deserialize<T3>(wrapperTag3);
            return (data1, data2, data3);
        }

        private static T Deserialize<T>(this JsonElement[] array, string wrapperTag)
        {
            return array.DeserializeArray<T>(wrapperTag).FirstOrDefault();
        }

        private static IEnumerable<T> DeserializeArray<T>(this JsonElement[] array, string wrapperTag)
        {
            foreach(var x in array)
            {
                if (x.TryGetProperty(wrapperTag, out var property))
                {
                    var data = JsonSerializer.Deserialize<T>(property.ToString());
                    yield return data;
                }
            }
        }

        private static async Task<JsonElement[]> GetResponse(this HttpResponseMessage response)
        {
            var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(doc.RootElement.ToString());
            }
            response.EnsureSuccessStatusCode();
            return doc.RootElement.GetProperty("Response").EnumerateArray().ToArray();
        }
    }
}