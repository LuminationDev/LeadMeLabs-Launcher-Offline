﻿using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using OfflineInstaller.Components._notification;
using OfflineInstaller.MVC.View;

namespace OfflineInstaller.Components._managers;

public static class ServerManager
{
    private static HttpListener? _listener;

    ///<summary>
    /// Starts a server using the HttpListener class to handle incoming HTTP requests.
    ///</summary>
    public static void StartServer()
    {
        MockConsole.WriteLine("Starting server.");

        _listener = new HttpListener();
        _listener.Prefixes.Add("http://+:8088/");

        try
        {
            _listener.Start();
            MockConsole.WriteLine("Server started. Listening for requests...");

            // Start a new thread to handle incoming requests
            var listenerThread = new System.Threading.Thread(Listen);
            listenerThread.Start();
        }
        catch (Exception ex)
        {
            MockConsole.WriteLine("An error occurred while starting the server: " + ex.Message);
        }
    }

    ///<summary>
    /// Listens for incoming requests and handles them using the HttpListener class.
    ///</summary>
    private static void Listen()
    {
        if (_listener == null) return;

        while (_listener.IsListening)
        {
            try
            {
                MockConsole.WriteLine("Waiting for new connection.");
                HttpListenerContext context = _listener.GetContext();
                Task.Run(() => HandleRequest(context));
            }
            catch (Exception ex)
            {
                MockConsole.WriteLine("An error occurred while handling the request: " + ex.Message);
            }
        }
    }
    
    /// <summary>
    /// Handles incoming HTTP requests and routes them to different actions based on the requested URL.
    /// </summary>
    /// <param name="context">The HttpListenerContext object representing the current request.</param>
    private static void HandleRequest(HttpListenerContext context)
    {
        string requestUrl = context.Request.Url.AbsolutePath.ToLower(); // Get the requested URL path in lowercase

        // Handle different routes based on the requested URL
        switch (requestUrl)
        {
            case "/":
                // Handle root route ("/")
                SendResponse(context, "Hello, World!");
                break;

            case "/nuc/nuc.zip":
            case "/program-nuc":
                MockConsole.WriteLine("NUC file being served.");
                ServeProgram(context, "NUC");
                break;

            case "/nuc/version":
            case "/program-nuc-version":
                MockConsole.WriteLine("NUC version being checked.");
                ServeProgramVersion(context, "nuc");
                break;

            case "/station/station.zip":
            case "/program-station":
                MockConsole.WriteLine("Station file being served.");
                ServeProgram(context, "Station");
                break;

            case "/station/version":
            case "/program-station-version":
                MockConsole.WriteLine("Station version being checked.");
                ServeProgramVersion(context, "station");
                break;

            case "/program-setvol":
                MockConsole.WriteLine("SetVol file being served.");
                ServeProgram(context, "SetVol");
                break;

            case "/program-steamcmd":
                MockConsole.WriteLine("SteamCMD file being served.");
                ServeProgram(context, "steamcmd");
                break;

            default:
                if (requestUrl.StartsWith("/static/electron-launcher"))
                {
                    MockConsole.WriteLine("Electron file being served.");
                    ServeStaticFile(context, "_programs/electron-launcher");
                }
                else
                {
                    // Handle unknown route
                    SendResponse(context, "404 Not Found", HttpStatusCode.NotFound);
                }
                break;
        }
    }

    /// <summary>
    /// Serves the program version information to an HTTP listener context.
    /// </summary>
    /// <param name="context">The HTTP listener context.</param>
    /// <param name="programName">The name of the program.</param>
    private static void ServeProgramVersion(HttpListenerContext context, string programName)
    {
        string? version = MainWindow.Instance.VersionMap[programName];
        if (version == null || string.IsNullOrWhiteSpace(version))
        {
            SendResponse(context, "404 Not Found", HttpStatusCode.NotFound);
            return;
        }

        if (version.Equals("Not found", StringComparison.OrdinalIgnoreCase) ||
            version.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            SendResponse(context, "404 Not Found", HttpStatusCode.NotFound);
            return;
        }

        string response = $"{version} {programName}";
        SendResponse(context, response);
    }

    ///<summary>
    /// Sends an HTTP response with the specified response string and status code.
    ///</summary>
    ///<param name="context">The HttpListenerContext representing the response.</param>
    ///<param name="responseString">The string to be sent as the response.</param>
    ///<param name="statusCode">The HTTP status code to be included in the response. Defaults to HttpStatusCode.OK if not specified.</param>
    private static void SendResponse(HttpListenerContext context, string responseString, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentLength64 = buffer.Length;
        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.OutputStream.Close();
        context.Response.Close();
    }

    ///<summary>
    /// Serves a program file as a downloadable attachment in the HTTP response.
    ///</summary>
    ///<param name="context">The HttpListenerContext representing the response.</param>
    ///<param name="programName">The name of the program file to be served.</param>
    private static void ServeProgram(HttpListenerContext context, string programName)
    {
        string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_programs", $"{programName}.zip");

        context.Response.ContentType = "application/octet-stream";
        context.Response.ContentLength64 = new FileInfo(file).Length;
        context.Response.AddHeader("Content-Disposition", $"attachment; filename=\"{programName}.zip\"");

        using (FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
        {
            //byte[] buffer = new byte[16384];
            byte[] buffer = new byte[32768];
            int bytesRead;

            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                context.Response.OutputStream.Write(buffer, 0, bytesRead);
            }
        }

        context.Response.Close();
    }

    ///<summary>
    /// Serves a static file as a response, identified by the relative path.
    ///</summary>
    ///<param name="context">The HttpListenerContext representing the response.</param>
    ///<param name="relativePath">The relative path of the static file to be served.</param>
    private static void ServeStaticFile(HttpListenerContext context, string relativePath)
    {
        string file = string.Concat(MainWindow.InstallerLocation, @"\" , relativePath, context.Request.Url.LocalPath.AsSpan("/static/electron-launcher".Length));
        if (File.Exists(file))
        {
            context.Response.ContentType = GetMimeType(file);
            context.Response.ContentLength64 = new FileInfo(file).Length;

            using (FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                //byte[] buffer = new byte[4096];
                byte[] buffer = new byte[32768];
                int bytesRead;

                while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    context.Response.OutputStream.Write(buffer, 0, bytesRead);
                }
            }

            context.Response.Close();
        }
        else
        {
            SendResponse(context, "404 Not Found", HttpStatusCode.NotFound);
        }
    }

    ///<summary>
    /// Retrieves the MIME type (content type) based on the file extension of the given file.
    ///</summary>
    ///<param name="fileName">The name of the file to retrieve the MIME type for.</param>
    ///<returns>The MIME type (content type) as a string.</returns>
    private static string GetMimeType(string fileName)
    {
        string extension = Path.GetExtension(fileName);

        switch (extension)
        {
            case ".html":
                return "text/html";
            case ".css":
                return "text/css";
            case ".js":
                return "application/javascript";
            case ".jpg":
            case ".jpeg":
                return "image/jpeg";
            case ".png":
                return "image/png";
            case ".yml":
                return "text/yaml";
            case ".blockmap": //This may not be correct
                return "application/json";
            default:
                return "application/octet-stream";
        }
    }

    ///<summary>
    /// Stops the HTTP server if it is currently running.
    ///</summary>
    public static void StopServer()
    {
        if (_listener != null && _listener.IsListening)
        {
            MockConsole.WriteLine("Stopping server.");
            _listener.Stop();
            MockConsole.WriteLine("Server stopped.");
        }
        else
        {
            MockConsole.WriteLine("Server is not running.");
        }
    }
}
