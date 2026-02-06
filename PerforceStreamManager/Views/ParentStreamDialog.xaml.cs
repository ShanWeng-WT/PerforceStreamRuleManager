using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using PerforceStreamManager.Services;

namespace PerforceStreamManager.Views;

public partial class ParentStreamDialog : Window
{
    private readonly P4Service _p4Service;
    private readonly string _streamPath;
    private readonly string? _currentParent;

    /// <summary>
    /// Gets the selected parent stream path (null for mainline/no parent)
    /// </summary>
    public string? SelectedParentPath { get; private set; }

    public ParentStreamDialog(P4Service p4Service, string streamPath, string? currentParent)
    {
        InitializeComponent();

        _p4Service = p4Service ?? throw new ArgumentNullException(nameof(p4Service));
        _streamPath = streamPath ?? throw new ArgumentNullException(nameof(streamPath));
        _currentParent = currentParent;

        // Set up the dialog
        StreamPathTextBox.Text = streamPath;
        CurrentParentTextBox.Text = string.IsNullOrEmpty(currentParent) ? "(none - mainline)" : currentParent;

        // Load available parent streams
        LoadAvailableParents();
    }

    private void LoadAvailableParents()
    {
        try
        {
            // Get all stream paths from P4
            var allStreams = _p4Service.GetAllStreamPaths();

            // Filter out the current stream (can't be its own parent)
            var availableParents = allStreams
                .Where(s => !string.Equals(s, _streamPath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Add an option for "no parent" (mainline)
            ParentComboBox.Items.Add("(none - mainline)");

            foreach (var stream in availableParents)
            {
                ParentComboBox.Items.Add(stream);
            }

            // Select the current parent if it exists
            if (string.IsNullOrEmpty(_currentParent))
            {
                ParentComboBox.SelectedIndex = 0; // Select "(none - mainline)"
            }
            else
            {
                // Try to find and select the current parent
                for (int i = 0; i < ParentComboBox.Items.Count; i++)
                {
                    if (string.Equals(ParentComboBox.Items[i]?.ToString(), _currentParent, StringComparison.OrdinalIgnoreCase))
                    {
                        ParentComboBox.SelectedIndex = i;
                        break;
                    }
                }

                // If not found, set the text directly (editable combobox)
                if (ParentComboBox.SelectedIndex < 0)
                {
                    ParentComboBox.Text = _currentParent;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load available streams: {ex.Message}",
                "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // Get the selected/entered parent path
        string? selectedValue = ParentComboBox.Text?.Trim();

        // Handle "no parent" selection
        if (string.IsNullOrEmpty(selectedValue) || selectedValue == "(none - mainline)")
        {
            SelectedParentPath = null;
        }
        else
        {
            // Validate the path starts with //
            if (!selectedValue.StartsWith("//"))
            {
                MessageBox.Show("Parent stream path must start with '//'",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if trying to set itself as parent
            if (string.Equals(selectedValue, _streamPath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("A stream cannot be its own parent.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedParentPath = selectedValue;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
