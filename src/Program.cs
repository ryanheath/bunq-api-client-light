using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;

using BunqClientLight.BunqModels;

namespace BunqClientLight
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("https://public-api.sandbox.bunq.com/v1/");

            var (publicKeyPem, privateKeyPem) = PublicPrivateKeys.Initialize();
            Console.WriteLine(publicKeyPem);
            Console.WriteLine(privateKeyPem);

            var apiKey = await GetSandboxApiKey(httpClient);
            var (installationToken, serverPublicKey) = await DoInstallation(httpClient, publicKeyPem);

            await SetupDevice(httpClient, apiKey, privateKeyPem, serverPublicKey, installationToken);

            var bunqApi = await SetupSession(httpClient, apiKey, privateKeyPem, serverPublicKey, installationToken);

            var account = await ListMonetaryAccountBanks(bunqApi);

            await GetSomeMoney(bunqApi, account.id);

            account = await ListMonetaryAccountBanks(bunqApi);

            await ListPayments(bunqApi, account.id);

            await SendSomeMoney(bunqApi, account.id, "30.25", "NL16BUNQ2025445040", "Veronika Mason");

            await ListPayments(bunqApi, account.id);

            await SendSomeMoney(bunqApi, account.id, "69.69", "GB33BUKB20201555555555", "A Bank");
            await SendSomeMoney(bunqApi, account.id, "123.45", "NL02ABNA0123456789", "A Bank NL");
            await SendSomeMoney(bunqApi, account.id, "200.00", "NL02ABNA0123456789", "A Bank NL");

            await GetSomeMoney(bunqApi, account.id);
            await SendSomeMoney(bunqApi, account.id, "500.00", "GB33BUKB20201555555555", "A Bank");
            await GetSomeMoney(bunqApi, account.id);
            // daily limit reached ... await SendSomeMoney(bunqApi, account.id, "500.00", "NL02ABNA0123456789", "A Bank");

            await ListPayments(bunqApi, account.id);

            account = await ListMonetaryAccountBanks(bunqApi);
        }

        static async Task<string> GetSandboxApiKey(HttpClient httpClient)
        {
            var bunqApi = new BunqApiSetup(httpClient);
            var sandboxUser = await bunqApi.GetSandboxUser();
            return sandboxUser.api_key;
        }
    
        static async Task<(string installationToken, string serverPublicKey)> DoInstallation(HttpClient httpClient, string publicKeyPem)
        {
            var bunqApi = new BunqApiSetup(httpClient);
            var installation = await bunqApi.Installation(publicKeyPem);
            return (installation.token.token, installation.serverPublicKey.server_public_key);
        }

        static async Task SetupDevice(HttpClient httpClient, string apiKey, string privateKeyPem, string serverPublicKey, string installationToken)
        {
            var bunqApi = new BunqApi(httpClient, apiKey, privateKeyPem, serverPublicKey, installationToken);
            await bunqApi.SetupDevice();
        }

        static async Task<BunqApi> SetupSession(HttpClient httpClient, string apiKey, string privateKeyPem, string serverPublicKey, string installationToken)
        {
            var bunqApi = new BunqApi(httpClient, apiKey, privateKeyPem, serverPublicKey, installationToken);
            await bunqApi.SetupSession();
            return bunqApi;
        }

        static async Task<MonetaryAccountBank> ListMonetaryAccountBanks(BunqApi bunqApi)
        {
            var accounts = await bunqApi.ListMonetaryAccountBanks();
            foreach(var account in accounts)
            {
                Console.WriteLine($"{account.id} {account.balance.currency} {account.balance.value} {account.status}");
            }
            return accounts.FirstOrDefault(x => x.status == "ACTIVE");
        }

        static async Task GetSomeMoney(BunqApi bunqApi, long monetaryAccountId)
        {
            var amount = new Amount { currency = "EUR", value = "500.00" }; // not more than 500
            var counterparty = new Pointer { value = "sugardaddy@bunq.com", type = "EMAIL" };
            await bunqApi.RequireInquiry(amount, counterparty, "get me some", monetaryAccountId);
            await Task.Delay(1000); // give server some time to process
        }

        static async Task ListPayments(BunqApi bunqApi, long monetaryAccountId)
        {
            var payments = await bunqApi.ListPayments(monetaryAccountId);
            foreach(var payment in payments)
            {
                Console.WriteLine($"{payment.id} ");
            }
        }

        static async Task SendSomeMoney(BunqApi bunqApi, long monetaryAccountId, string value, string iban, string name)
        {
            var amount = new Amount { currency = "EUR", value = value };
            var counterparty = new Pointer { value = iban, type = "IBAN", name = name };
            await bunqApi.Payment(amount, counterparty, "sent you some", monetaryAccountId);
        }
    }
}
