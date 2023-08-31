using System.Net.Http;

namespace OfflineInstaller._managers
{
    public class NetworkManager
    {
        /// <summary>
        /// Query if the computer can ping the URL of google. This shows if the user can access the internet of not.
        /// </summary>
        /// <returns></returns>
        public static bool GetInternetAccess()
        {
            bool hasInternetAccess = CheckInternetAccess();

            if (hasInternetAccess)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        static bool CheckInternetAccess()
        {
            using HttpClient client = new();
            try
            {
                HttpResponseMessage response = client.GetAsync("https://herokuapp.com").Result;
                return response.IsSuccessStatusCode || response.StatusCode.ToString() == "Forbidden";
            }
            catch
            {
                return false;
            }
        }
    }
}
