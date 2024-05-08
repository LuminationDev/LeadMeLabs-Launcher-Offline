using System;
using System.Net.Http;

namespace OfflineInstaller.Components._utils;

public static class NetworkManager
{
    /// <summary>
    /// Query if the computer can ping the URL of google. This shows if the user can access the internet of not.
    /// </summary>
    /// <returns></returns>
    public static bool GetInternetAccess()
    {
        bool hasInternetAccess = CheckInternetAccess();
        return hasInternetAccess;
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

    public static bool CheckVultrAccess()
    {
        using HttpClient client = new();
        try
        {
            HttpResponseMessage response = client.GetAsync("https://leadme-healthcheck.sgp1.vultrobjects.com/healthcheck").Result;
            return response.IsSuccessStatusCode || response.StatusCode.ToString() == "Forbidden";
        }
        catch
        {
            return false;
        }
    }
}
