﻿using System;
using System.Windows;
using OfflineInstaller.MVC.ViewModel;

namespace OfflineInstaller.MVC.View;

/// <summary>
/// Interaction logic for LoadingWindow.xaml
/// </summary>
public partial class ConfirmWindow : Window, IDisposable
{
    public ConfirmWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose of any managed resources here
            // Example: Dispose of child controls, timers, etc.
        }

        // Dispose of any unmanaged resources here
        // Example: Close file handles, database connections, etc.
    }
    
    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        // Set DialogResult to true to indicate a positive result
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        // Set DialogResult to false to indicate a negative result
        DialogResult = false;
    }
}
