using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DeezFiles.Services
{
    public class AuthorizationService
    {
        public static string accountAddress;
        public static string nodeAddress;
        public static string currentUsername;
        public static string currentPassword;

        public static string GetLocalIPAddress()
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Skip unwanted adapters
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    ni.Description.ToLower().Contains("virtual") ||
                    ni.Description.ToLower().Contains("vmware") ||
                    ni.Description.ToLower().Contains("hyper-v"))
                    continue;

                var ipProps = ni.GetIPProperties();

                // MUST have gateway → real internet connection
                if (ipProps.GatewayAddresses.Count == 0)
                    continue;

                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return addr.Address.ToString(); // ✅ correct WiFi IP
                    }
                }
            }

            throw new Exception("No valid network adapter found");
        }

        public static async Task<bool> MakeNodeOnline()
        {
            string localIP = GetLocalIPAddress();

            var goonline = new
            {
                DNAddress = nodeAddress,
                IPAddress = localIP,
                Port = 5000
            };

            string goOnlinejson = JsonConvert.SerializeObject(goonline);
            var goOnlinecontent = new StringContent(goOnlinejson, Encoding.UTF8, "application/json");

            HttpResponseMessage onlineResponse =
                await NetworkService.SendPostRequest("OnlineNodes/GoOnline", goOnlinecontent);

            return onlineResponse.IsSuccessStatusCode;
        }

        public static async Task<string?> LoginUser(string username, string password)
        {
            var loginuser = new
            {
                username = username,
                password = password
            };

            string jsondata = JsonConvert.SerializeObject(loginuser);
            var content = new StringContent(jsondata, Encoding.UTF8, "application/json");

            HttpResponseMessage response =
                await NetworkService.SendPostRequest("LoginInfoes/login", content);

            // ❌ If login fails (401 etc.)
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            // ✅ Login success
            string selfAddress = await response.Content.ReadAsStringAsync();
            accountAddress = selfAddress;

            return selfAddress;
        }

        public static string CreateDeviceNodeAddress()
        {
            string machineName = Environment.MachineName;
            string userName = Environment.UserName;
            string rawDeviceId = $"{machineName}-{userName}".ToLowerInvariant();
            string deviceId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(rawDeviceId)))
                .Substring(0, 12)
                .ToLowerInvariant();

            nodeAddress = $"{accountAddress}:{deviceId}";
            return nodeAddress;
        }

        public static async Task<HttpResponseMessage> RegisterUser(string username, string password, string email)
        {
            var newUser = new
            {
                Username = username,
                EmailId = email,
                Password = password
                // ❗ Removed DNAddress → backend generates it
            };

            string jsondata = JsonConvert.SerializeObject(newUser);
            var content = new StringContent(jsondata, Encoding.UTF8, "application/json");

            HttpResponseMessage postresponse =
                await NetworkService.SendPostRequest("LoginInfoes/register", content);

            return postresponse;
        }

        public static async Task<HttpResponseMessage> Logout()
        {
            var offlineData = new
            {
                DNAddress = nodeAddress
            };

            string offlineJson = JsonConvert.SerializeObject(offlineData);
            var content = new StringContent(offlineJson, Encoding.UTF8, "application/json");

            HttpResponseMessage response =
                await NetworkService.SendPostRequest("OnlineNodes/GoOffline", content);

            return response;
        }
    }
}
