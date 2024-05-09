using System;
using System.Diagnostics;
using NetFwTypeLib;
using OfflineInstaller.Components._notification;

namespace OfflineInstaller.Components._managers;

public static class FirewallManager
{
    /// <summary>
    /// Checks if the program at the current executable path is allowed through the firewall.
    /// </summary>
    /// <returns>
    /// Returns "Allowed" if the program is allowed through the firewall,
    /// otherwise returns "Not allowed".
    /// </returns>
    public static string IsProgramAllowedThroughFirewall()
    {
        string? programPath = GetExecutablePath();

        INetFwPolicy2? firewallPolicy = GetFirewallPolicy();

        if (firewallPolicy == null)
        {
            return "Unknown";
        }

        NET_FW_ACTION_ action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;

        foreach (INetFwRule rule in firewallPolicy.Rules)
        {
            if (rule.ApplicationName != null)
            {
                if (rule.Action == action && rule.ApplicationName.Equals(programPath, StringComparison.OrdinalIgnoreCase))
                {
                    return "Allowed";
                }
            }
        }

        return "Not allowed";
    }

    /// <summary>
    /// Retrieves the firewall policy using the HNetCfg.FwPolicy2 COM object.
    /// </summary>
    /// <returns>The INetFwPolicy2 object representing the firewall policy.</returns>
    private static INetFwPolicy2? GetFirewallPolicy()
    {
        Type? type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
        if (type == null) return null;
        return Activator.CreateInstance(type) as INetFwPolicy2;
    }

    /// <summary>
    /// Retrieves the path of the current executable.
    /// </summary>
    /// <returns>The path of the current executable, or null if it cannot be determined.</returns>
    private static string? GetExecutablePath()
    {
        string? executablePath = null;

        try
        {
            Process currentProcess = Process.GetCurrentProcess();
            if (currentProcess != null)
            {
                executablePath = currentProcess.MainModule.FileName;
            }
        }
        catch
        {
            // Handle any exceptions that occur during path retrieval
        }

        return executablePath;
    }

    /// <summary>
    /// Check if the port is allowed within the Inbound Rules. This program requires port 8088 to be allowed for a TCP within
    /// the firewall.
    /// </summary>
    public static bool IsPortAllowed()
    {
        int port = 8088;

        INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
        INetFwRules firewallRules = firewallPolicy.Rules;

        foreach (INetFwRule rule in firewallRules)
        {
            if (rule.Protocol == (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP &&
                rule.LocalPorts == port.ToString() &&
                rule.Enabled)
            {
                return true;
            }
        }

        return false;
    }

    public static void CreateInboundRule()
    {
        // Create an instance of the firewall manager
        var type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
        if (type == null)
        {
            MockConsole.WriteLine($"Cannot auto create rule, please manually create it.");
            return;
        }
        
        var firewall = Activator.CreateInstance(type) as INetFwPolicy2;

        if (firewall == null)
        {
            MockConsole.WriteLine($"Cannot auto create rule, please manually create it.");
            return;
        }

        // Create a new firewall rule
        var rule = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule")) as INetFwRule2;
        
        if (rule == null)
        {
            MockConsole.WriteLine($"Cannot auto create rule, please manually create it.");
            return;
        }

        // Set the rule properties
        rule.Name = "OfflineInstaller";
        rule.Description = "Allow incoming connections on port 8088";
        rule.Protocol = (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP;
        rule.LocalPorts = "8088";
        rule.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
        rule.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;
        rule.Enabled = true;

        // Set the rule to apply to all profiles (domain, private, public)
        rule.Profiles = (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_ALL;

        // Add the rule to the firewall
        firewall.Rules.Add(rule);
    }
}
