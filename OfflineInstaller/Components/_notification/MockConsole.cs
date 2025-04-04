﻿using System;
using OfflineInstaller.MVC.ViewModel;

namespace OfflineInstaller.Components._notification;

public static class MockConsole
{
    private static MainWindowViewModel? _viewModel;

    public static void SetViewModel(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    /// <summary>
    /// Describe the different levels of logging, only the most essential messages are printed at None.
    /// The levels are [None - essential only, Normal - basic messages and commands, Debug - anything that can be used for information, Verbose - everything].
    /// </summary>
    public enum LogLevel
    {
        Off,
        Error,
        Normal,
        Debug,
        Verbose
    }

    private static readonly int _lineLimit = 100;
    public static LogLevel _logLevel = LogLevel.Normal;


    //The functions below handle updating the mock console that is present within the MainWindow. This
    //process allows other parts of the project to display information to a user.

    /// <summary>
    /// Clear the MockConsole of all previous messages. The cleared message will be printed regardless
    /// of log level as to alert the user this is deliberate.
    /// </summary>
    public static void ClearConsole()
    {
        if (_viewModel == null) return;
        _viewModel.ConsoleText = "";
        WriteLine("Cleared", LogLevel.Error);
    }

    /// <summary>
    /// This is only to be used for the DLL library callback.
    /// Log a message to the mock console within the Station form, this does not take into account the current log level.
    /// </summary>
    /// <param name="message">A string to be printed to the console.</param>
    public static void WriteLine(string message)
    {
        if (message.Trim() == "" || _viewModel == null) return;
        if (_logLevel == LogLevel.Off) return;

        if (_viewModel.ConsoleText != null)
        {
            var lines = _viewModel.ConsoleText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length >= _lineLimit)
            {
                var newLines = new string[_lineLimit];
                Array.Copy(lines, 1, newLines, 0, newLines.Length - 1);
                newLines[^1] = string.Empty;
                _viewModel.ConsoleText = string.Join("\n", newLines);
            }
        }

        _viewModel.ConsoleText = _viewModel.ConsoleText + DateStamp() + message + "\n";
    }

    /// <summary>
    /// Log a message to the mock console within the Station form, only print it if it conforms to the current logging level.
    /// </summary>
    /// <param name="message">A string to be printed to the console.</param>
    /// <param name="level">A Loglevel enum representing if it should be displayed at the current logging level.</param>
    private static void WriteLine(string message, LogLevel level)
    {
        if (message.Trim() == "" || _viewModel == null) return;
        if (level > _logLevel || _logLevel == LogLevel.Off) return;


        if (_viewModel.ConsoleText != null)
        {
            var lines = _viewModel.ConsoleText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length >= _lineLimit)
            {
                var newLines = new string[_lineLimit];
                Array.Copy(lines, 1, newLines, 0, newLines.Length - 1);
                newLines[^1] = string.Empty;
                _viewModel.ConsoleText = string.Join("\n", newLines);
            }
        }

        _viewModel.ConsoleText = _viewModel.ConsoleText + DateStamp() + message + "\n";
    }

    private static string DateStamp()
    {
        DateTime now = DateTime.Now;
        return $"[{now:dd/MM | hh:mm:ss}] ";
    }
}
