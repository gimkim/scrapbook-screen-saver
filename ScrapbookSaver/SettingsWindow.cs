using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Color = System.Windows.Media.Color;
using ComboBox = System.Windows.Controls.ComboBox;
using FontFamily = System.Windows.Media.FontFamily;
using GroupBox = System.Windows.Controls.GroupBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ListBox = System.Windows.Controls.ListBox;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using SelectionMode = System.Windows.Controls.SelectionMode;
using TextBox = System.Windows.Controls.TextBox;

namespace ScrapbookSaver;

internal sealed class SettingsWindow : Window
{
    private readonly SaverOptions options;
    private readonly ListBox folders = new() { MinHeight = 112, MaxHeight = 170, SelectionMode = SelectionMode.Extended };
    private readonly Dictionary<string, TextBox> fields = [];
    private readonly ComboBox animation = new() { MinWidth = 170 };
    private readonly CheckBox shadow = new() { Content = "Enable drop shadow" };
    private readonly Grid root = new() { Background = Brushes.White };
    private readonly ScrollViewer scroller = new();

    public SettingsWindow(SaverOptions options)
    {
        this.options = options;
        Title = "Scrapbook Screen Saver Settings";
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 14;
        Background = Brushes.White;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.CanResize;
        var work = SystemParameters.WorkArea;
        Width = Math.Max(280, Math.Min(820, work.Width * 0.9));
        Height = Math.Max(260, Math.Min(860, work.Height * 0.9));
        MinWidth = Math.Min(300, Width);
        MinHeight = Math.Min(260, Height);

        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;

        var content = new StackPanel { Orientation = Orientation.Vertical };
        scroller.Content = content;
        scroller.Margin = new Thickness(20, 20, 20, 0);
        scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        scroller.CanContentScroll = false;
        Grid.SetRow(scroller, 0);
        root.Children.Add(scroller);

        content.Children.Add(CreateFoldersSection());
        content.Children.Add(CreateAppearanceSection());
        content.Children.Add(CreateAnimationSection());
        content.Children.Add(new TextBlock
        {
            Text = "Photo width is a percentage of screen width. Edge overflow 0% keeps cards fully on screen; 10% permits up to 10% outside. A shorter animation time is faster.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(5, 8, 12, 12)
        });

        var footer = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(20, 12, 12, 20)
        };
        var preview = MakeButton("Preview");
        preview.Click += (_, _) =>
        {
            if (!SaveSettings(false)) return;
            new PreviewWindow(options, this).ShowDialog();
        };
        var save = MakeButton("Save");
        save.IsDefault = true;
        save.Click += (_, _) => SaveSettings(true);
        var cancel = MakeButton("Cancel");
        cancel.IsCancel = true;
        cancel.Click += (_, _) => Close();
        footer.Children.Add(preview);
        footer.Children.Add(save);
        footer.Children.Add(cancel);
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);
    }

    private GroupBox CreateFoldersSection()
    {
        foreach (var folder in options.ImageFolders) folders.Items.Add(FolderCheckBox(folder));
        ScrollViewer.SetHorizontalScrollBarVisibility(folders, ScrollBarVisibility.Auto);
        var layout = new Grid { Margin = new Thickness(10) };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(folders, 0);
        layout.Children.Add(folders);

        var actions = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
        var add = MakeButton("Add...");
        add.Click += (_, _) =>
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                InitialDirectory = (folders.SelectedItem as CheckBox)?.Tag as string ?? options.Folder,
                Title = "Choose an image folder"
            };
            if (dialog.ShowDialog(this) != true) return;
            if (folders.Items.Cast<CheckBox>().Any(item => string.Equals((string)item.Tag, dialog.FolderName, StringComparison.OrdinalIgnoreCase))) return;
            var added = FolderCheckBox(new ImageFolderSetting { Path = dialog.FolderName });
            folders.Items.Add(added);
            folders.SelectedItem = added;
        };
        var remove = MakeButton("Remove");
        remove.Click += (_, _) =>
        {
            foreach (var item in folders.SelectedItems.Cast<object>().ToArray()) folders.Items.Remove(item);
        };
        actions.Children.Add(add);
        actions.Children.Add(remove);
        Grid.SetColumn(actions, 1);
        layout.Children.Add(actions);
        return Section("Image folders (check to use)", layout);
    }

    private CheckBox FolderCheckBox(ImageFolderSetting folder)
    {
        var item = new CheckBox
        {
            Content = folder.Path,
            Tag = folder.Path,
            ToolTip = folder.Path,
            IsChecked = folder.Enabled,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 4, 4, 4)
        };
        item.Click += (_, _) => folders.SelectedItem = item;
        return item;
    }

    private GroupBox CreateAppearanceSection()
    {
        var grid = TwoColumnGrid();
        AddField(grid, "Photo interval (sec)", "interval", options.IntervalSeconds.ToString(CultureInfo.InvariantCulture));
        AddField(grid, "Min. photo width (%)", "minSize", options.MinWidthPercent.ToString(CultureInfo.InvariantCulture));
        AddField(grid, "Max. photo width (%)", "maxSize", options.MaxWidthPercent.ToString(CultureInfo.InvariantCulture));
        AddField(grid, "Edge overflow (%)", "edgeOverflow", options.EdgeOverflowPercent.ToString(CultureInfo.InvariantCulture));
        AddField(grid, "White border (px)", "border", options.BorderPixels.ToString(CultureInfo.InvariantCulture));
        AddField(grid, "Maximum tilt (degrees)", "tilt", options.MaxTiltDegrees.ToString(CultureInfo.InvariantCulture));
        AddField(grid, "Max. photos on screen", "count", options.MaxPhotos.ToString(CultureInfo.InvariantCulture));
        shadow.IsChecked = options.DropShadow;
        AddRow(grid, "Shadow", shadow);
        return Section("Appearance", grid);
    }

    private GroupBox CreateAnimationSection()
    {
        var grid = TwoColumnGrid();
        animation.ItemsSource = new[] { "None", "Fade", "Slide in", "Zoom from center", "Random for each photo" };
        animation.SelectedIndex = (int)options.Animation;
        AddRow(grid, "Entrance animation", animation);
        AddField(grid, "Animation time (sec)", "animationSeconds", (options.AnimationDurationMs / 1000m).ToString("0.0", CultureInfo.InvariantCulture));
        return Section("Animation", grid);
    }

    private static Grid TwoColumnGrid()
    {
        var grid = new Grid { Margin = new Thickness(10, 5, 10, 5) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 90 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star), MinWidth = 100 });
        return grid;
    }

    private void AddField(Grid grid, string label, string key, string value)
    {
        var field = new TextBox
        {
            Text = value,
            Width = 94,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(7, 5, 7, 5)
        };
        fields.Add(key, field);
        AddRow(grid, label, field);
    }

    private static void AddRow(Grid grid, string label, UIElement control)
    {
        var row = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var title = new TextBlock
        {
            Text = label,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 7, 12, 7)
        };
        Grid.SetRow(title, row);
        Grid.SetColumn(title, 0);
        grid.Children.Add(title);
        if (control is FrameworkElement element) element.Margin = new Thickness(0, 5, 0, 5);
        Grid.SetRow(control, row);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
    }

    private static GroupBox Section(string title, UIElement content) => new()
    {
        Header = title,
        Content = content,
        Margin = new Thickness(0, 0, 10, 12),
        Padding = new Thickness(4)
    };

    private static Button MakeButton(string label) => new()
    {
        Content = label,
        MinWidth = 90,
        Padding = new Thickness(13, 7, 13, 7),
        Margin = new Thickness(0, 0, 8, 8)
    };

    private bool SaveSettings(bool close)
    {
        var selectedFolders = folders.Items.Cast<CheckBox>().Select(item => new ImageFolderSetting
        {
            Path = (string)item.Tag,
            Enabled = item.IsChecked == true
        }).ToList();
        if (selectedFolders.Count == 0) return Error("Add at least one image folder.");
        var missingFolder = selectedFolders.FirstOrDefault(folder => folder.Enabled && !Directory.Exists(folder.Path));
        if (missingFolder != null) return Error($"Image folder not found:\n{missingFolder.Path}");
        if (!ReadInt("interval", 1, 120, out var interval)) return false;
        if (!ReadInt("minSize", 5, 70, out var minSize)) return false;
        if (!ReadInt("maxSize", 5, 80, out var maxSize)) return false;
        if (minSize > maxSize) return Error("Minimum photo width cannot exceed maximum photo width.");
        if (!ReadInt("edgeOverflow", 0, 25, out var edgeOverflow)) return false;
        if (!ReadInt("border", 0, 30, out var border)) return false;
        if (!ReadInt("tilt", 0, 20, out var tilt)) return false;
        if (!ReadInt("count", 1, 100, out var count)) return false;
        if (!decimal.TryParse(fields["animationSeconds"].Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds)
            || seconds < 0.2m || seconds > 5m)
            return InvalidField("animationSeconds", "Animation time must be between 0.2 and 5 seconds.");

        options.ImageFolders = selectedFolders;
        options.IntervalSeconds = interval;
        options.MinWidthPercent = minSize;
        options.MaxWidthPercent = maxSize;
        options.EdgeOverflowPercent = edgeOverflow;
        options.BorderPixels = border;
        options.MaxTiltDegrees = tilt;
        options.MaxPhotos = count;
        options.DropShadow = shadow.IsChecked == true;
        options.Animation = (EntranceAnimation)animation.SelectedIndex;
        options.AnimationDurationMs = (int)(seconds * 1000);
        options.Save();
        if (close) Close();
        return true;
    }

    private bool ReadInt(string key, int min, int max, out int value)
    {
        if (int.TryParse(fields[key].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value >= min && value <= max) return true;
        return InvalidField(key, $"Enter a whole number from {min} to {max}.");
    }

    private bool InvalidField(string key, string message)
    {
        fields[key].Focus();
        fields[key].SelectAll();
        return Error(message);
    }

    private bool Error(string message)
    {
        MessageBox.Show(this, message, "Check settings", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    public void SaveSnapshot(string path, bool bottom = false)
    {
        root.UpdateLayout();
        if (bottom)
        {
            scroller.ScrollToEnd();
            root.UpdateLayout();
        }
        var width = Math.Max(1, (int)Math.Ceiling(root.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(root.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}

internal sealed class PreviewWindow : Window
{
    private readonly GpuCollageView view;
    private readonly DispatcherTimer timer;

    public PreviewWindow(SaverOptions options, Window owner)
    {
        Title = "Scrapbook Screen Saver Preview — Press Esc to close";
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = Math.Min(960, owner.ActualWidth * 0.9);
        Height = Math.Min(640, owner.ActualHeight * 0.85);
        MinWidth = 320;
        MinHeight = 240;
        Background = new SolidColorBrush(Color.FromRgb(24, 25, 29));
        view = new GpuCollageView(options);
        Content = view.Root;
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(options.IntervalSeconds) };
        timer.Tick += (_, _) => view.RequestNext();
        Loaded += (_, _) => { view.RequestNext(); timer.Start(); view.Root.Focus(); };
        Closed += (_, _) => { timer.Stop(); view.Dispose(); };
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
    }

    public void SaveSnapshot(string destination) => view.SaveSnapshot(destination);
}
