using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using PerforceStreamManager.Services;
using Perforce.P4;

namespace PerforceStreamManager.Views;

public partial class DepotBrowserDialog : Window
{
    private readonly P4Service _p4Service;
    private readonly ErrorMessageSanitizer _errorSanitizer;
    private readonly string _rootPath;
    private readonly string _targetStream;

    public string SelectedPath { get; private set; }

    public DepotBrowserDialog(P4Service p4Service, string rootPath = null, string targetStream = null)
    {
        InitializeComponent();
        _p4Service = p4Service;
        _errorSanitizer = new ErrorMessageSanitizer(p4Service.Logger);
        _rootPath = rootPath;
        _targetStream = targetStream;

        Loaded += DepotBrowserDialog_Loaded;
    }
    
    private async void DepotBrowserDialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusTextBlock.Text = "Loading depot structure...";
            await LoadDepotStructure();
            StatusTextBlock.Text = "Ready";
        }
        catch (Exception ex)
        {
            string safeMessage = _errorSanitizer.SanitizeForUserSimple(ex, "DepotBrowserDialog_Loaded");
            StatusTextBlock.Text = "Error loading depot structure";
            MessageBox.Show(safeMessage,
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private async Task LoadDepotStructure()
    {
        await Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrEmpty(_rootPath))
                {
                    // Get root depot directories
                    var directories = _p4Service.GetDepotDirectories("//");
                    
                    Dispatcher.Invoke(() =>
                    {
                        DepotTreeView.Items.Clear();
                        
                        foreach (var dir in directories)
                        {
                            var node = new DepotNode
                            {
                                Name = dir,
                                Path = dir,
                                IsDirectory = true
                            };
                            
                            // Add a dummy child to enable expansion
                            node.Children.Add(new DepotNode { Name = "Loading..." });
                            
                            DepotTreeView.Items.Add(node);
                        }
                    });
                }
                else
                {
                    // Use the provided root path as the single root node
                    Dispatcher.Invoke(() =>
                    {
                        DepotTreeView.Items.Clear();
                        
                        var node = new DepotNode
                        {
                            Name = _rootPath,
                            Path = _rootPath,
                            IsDirectory = true
                        };
                        
                        // Add a dummy child to enable expansion
                        node.Children.Add(new DepotNode { Name = "Loading..." });
                        
                        DepotTreeView.Items.Add(node);
                        
                        // Optionally expand the root node
                        // This would require finding the TreeViewItem container which isn't generated yet
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    throw new Exception($"Failed to load depot directories: {ex.Message}", ex);
                });
            }
        });
    }
    
    private void DepotTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is DepotNode selectedNode)
        {
            SelectedPath = selectedNode.Path.Contains(_targetStream) ? selectedNode.Path.Substring(_targetStream.Length + 1) : selectedNode.Path;
            SelectedPathTextBox.Text = SelectedPath;
            
            // Lazy load children if needed (in case selected without expanding first)
            if (selectedNode.Children.Count == 1 && selectedNode.Children[0].Name == "Loading...")
            {
                LoadChildren(selectedNode);
            }
        }
    }

    private void DepotTreeView_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem item && item.DataContext is DepotNode node)
        {
            // Lazy load children if needed
            if (node.Children.Count == 1 && node.Children[0].Name == "Loading...")
            {
                LoadChildren(node);
            }
        }
    }
    
    private async void LoadChildren(DepotNode parentNode)
    {
        try
        {
            StatusTextBlock.Text = $"Loading contents of {parentNode.Path}...";
            _p4Service.Logger.LogInfo($"[DepotBrowser] Loading children for: {parentNode.Path}");
            
            await Task.Run(() =>
            {
                var children = new List<DepotNode>();
                string searchPath = parentNode.Path;

                // Check if this is a virtual stream and resolve to physical backing stream if necessary
                try 
                {
                    var stream = _p4Service.GetStreamSafe(parentNode.Path);
                    if (stream != null && stream.Type == StreamType.Virtual)
                    {
                        _p4Service.Logger.LogInfo($"[DepotBrowser] Node {parentNode.Path} is a virtual stream. Resolving physical parent...");
                        
                        // Walk up the parent chain
                        var currentStream = stream;
                        while (currentStream != null && currentStream.Type == StreamType.Virtual && currentStream.Parent != null)
                        {
                            string parentPath = currentStream.Parent.ToString();
                             _p4Service.Logger.LogInfo($"[DepotBrowser] Checking parent: {parentPath}");
                            var parentStream = _p4Service.GetStreamSafe(parentPath);
                            
                            if (parentStream != null)
                            {
                                currentStream = parentStream;
                                if (currentStream.Type != StreamType.Virtual)
                                {
                                    searchPath = currentStream.Id;
                                    _p4Service.Logger.LogInfo($"[DepotBrowser] Resolved to physical stream: {searchPath}");
                                    break;
                                }
                            }
                            else
                            {
                                // Parent not found or not accessible
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _p4Service.Logger.LogError(ex, $"[DepotBrowser] Error resolving virtual stream for {parentNode.Path}");
                }

                // 1. Fetch Directories
                try
                {
                    _p4Service.Logger.LogInfo($"[DepotBrowser] Fetching directories for: {searchPath}");
                    var directories = _p4Service.GetDepotDirectories(searchPath);
                    _p4Service.Logger.LogInfo($"[DepotBrowser] Found {directories.Count} directories in {searchPath}");
                    
                    foreach (var dir in directories)
                    {
                        var childNode = new DepotNode
                        {
                            Name = System.IO.Path.GetFileName(dir.TrimEnd('/')),
                            Path = dir,
                            IsDirectory = true
                        };
                        
                        // Add dummy child to enable expansion
                        childNode.Children.Add(new DepotNode { Name = "Loading..." });
                        
                        children.Add(childNode);
                    }
                }
                catch (Exception ex)
                {
                    _p4Service.Logger.LogError(ex, $"[DepotBrowser] Failed to fetch directories for {searchPath}");
                    // Ignore errors fetching directories (e.g. no directories found)
                }

                // 2. Fetch Files
                try
                {
                    // Ensure path ends with /* for file listing
                    string fileSearchPath = searchPath.TrimEnd('/') + "/*";
                    _p4Service.Logger.LogInfo($"[DepotBrowser] Fetching files for: {fileSearchPath}");
                    
                    var files = _p4Service.GetDepotFiles(fileSearchPath);
                    _p4Service.Logger.LogInfo($"[DepotBrowser] Found {files.Count} files in {searchPath}");
                    
                    foreach (var file in files)
                    {
                        children.Add(new DepotNode
                        {
                            Name = System.IO.Path.GetFileName(file),
                            Path = file,
                            IsDirectory = false
                        });
                    }
                }
                catch (Exception ex)
                {
                    _p4Service.Logger.LogError(ex, $"[DepotBrowser] Failed to fetch files for {searchPath}");
                    // Ignore errors fetching files (e.g. no files found)
                }
                
                // 3. Check for Stream Paths (Virtual/Imported folders) - Use original path for this
                try
                {
                    var stream = _p4Service.GetStreamSafe(parentNode.Path);
                    if (stream != null && stream.Paths != null)
                    {
                        _p4Service.Logger.LogInfo($"[DepotBrowser] Node {parentNode.Path} is a stream. Parsing paths...");
                        
                        foreach (var mapEntry in stream.Paths)
                        {
                            if (mapEntry.Left != null)
                            {
                                string viewPath = mapEntry.Left.Path;
                                // viewPath is like "src/..." or "..." or "lib/foo/..."
                                
                                // We want the first component relative to the stream root
                                // If path is "...", it corresponds to the root itself, so we skip it (files already handled)
                                // If path is "src/...", we want "src"
                                
                                string cleanPath = viewPath.TrimEnd('.', '/'); // "src"
                                
                                if (!string.IsNullOrWhiteSpace(cleanPath))
                                {
                                    string[] parts = cleanPath.Split('/');
                                    string topFolder = parts[0];
                                    
                                    // Check if we already added this folder from physical directories
                                    if (!children.Any(c => c.Name.Equals(topFolder, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        _p4Service.Logger.LogInfo($"[DepotBrowser] Adding virtual folder from stream view: {topFolder}");
                                        
                                        var virtualNode = new DepotNode
                                        {
                                            Name = topFolder,
                                            Path = $"{parentNode.Path.TrimEnd('/')}/{topFolder}",
                                            IsDirectory = true
                                        };
                                        virtualNode.Children.Add(new DepotNode { Name = "Loading..." });
                                        children.Add(virtualNode);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                     _p4Service.Logger.LogError(ex, $"[DepotBrowser] Error parsing stream paths for {parentNode.Path}");
                }

                Dispatcher.Invoke(() =>
                {
                    parentNode.Children.Clear();
                    
                    foreach (var child in children)
                    {
                        parentNode.Children.Add(child);
                    }
                    
                    StatusTextBlock.Text = "Ready";
                    _p4Service.Logger.LogInfo($"[DepotBrowser] Successfully loaded {children.Count} items for {parentNode.Path}");
                });
            });
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Error: {ex.Message}";
            _p4Service.Logger.LogError(ex, $"[DepotBrowser] Fatal error in LoadChildren for {parentNode.Path}");
        }
    }
    
    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SelectedPath))
        {
            MessageBox.Show("Please select a path.", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
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

// Helper class for depot tree nodes
public class DepotNode
{
    public string Name { get; set; }
    public string Path { get; set; }
    public bool IsDirectory { get; set; }
    public ObservableCollection<DepotNode> Children { get; set; } = new ObservableCollection<DepotNode>();
}
