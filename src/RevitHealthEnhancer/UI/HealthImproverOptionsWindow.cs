using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using RevitHealthEnhancer.Core;

namespace RevitHealthEnhancer.UI
{
    public sealed class HealthImproverOptionsWindow : Window
    {
        private readonly HealthImproverOptions options;
        private readonly List<CheckBox> actionCheckBoxes = new List<CheckBox>();
        private readonly CheckBox confirmationCheckBox;
        private Button runButton;

        public HealthImproverOptionsWindow(HealthImproverOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));

            Title = "Revit Model Health Enhancer";
            Width = 620;
            MinWidth = 560;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(246, 248, 250));

            StackPanel root = new StackPanel
            {
                Margin = new Thickness(18)
            };

            root.Children.Add(CreateTitleText());
            root.Children.Add(CreateIntroText());
            root.Children.Add(CreateActionsPanel());

            confirmationCheckBox = new CheckBox
            {
                Margin = new Thickness(0, 14, 0, 8),
                Content = "I understand the selected fixes will modify the active Revit model.",
                FontWeight = FontWeights.SemiBold,
                IsChecked = false
            };
            confirmationCheckBox.Checked += OnSelectionChanged;
            confirmationCheckBox.Unchecked += OnSelectionChanged;
            root.Children.Add(confirmationCheckBox);

            root.Children.Add(CreateButtonPanel());

            Content = root;
            UpdateRunButtonState();
        }

        public static bool ShowDialogForOptions(HealthImproverOptions options, IntPtr ownerHandle)
        {
            HealthImproverOptionsWindow window = new HealthImproverOptionsWindow(options);

            if (ownerHandle != IntPtr.Zero)
            {
                WindowInteropHelper helper = new WindowInteropHelper(window);
                helper.Owner = ownerHandle;
            }

            bool? result = window.ShowDialog();
            return result == true;
        }

        private TextBlock CreateTitleText()
        {
            return new TextBlock
            {
                Text = "Select Model Health Actions",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                Margin = new Thickness(0, 0, 0, 8)
            };
        }

        private TextBlock CreateIntroText()
        {
            return new TextBlock
            {
                Text = "Choose exactly which checks and fixes should run. Unchecked items will be skipped.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(75, 85, 99)),
                Margin = new Thickness(0, 0, 0, 14)
            };
        }

        private Border CreateActionsPanel()
        {
            StackPanel panel = new StackPanel();

            AddAction(panel, "Pin levels, grids, Revit links and CAD links",
                "Protects key model control and coordination elements from accidental movement.",
                options.PinControlElements,
                value => options.PinControlElements = value);

            AddAction(panel, "Move levels and grids to Shared Levels and Grids workset",
                "Updates the workset assignment for levels and grids when the target workset exists.",
                options.FixLevelGridWorksets,
                value => options.FixLevelGridWorksets = value);

            AddAction(panel, "Delete invalid rooms",
                "Deletes rooms with no location or zero/invalid area.",
                options.DeleteInvalidRooms,
                value => options.DeleteInvalidRooms = value);

            AddAction(panel, "Delete invalid MEP spaces",
                "Deletes spaces with no location or zero/invalid area.",
                options.DeleteInvalidSpaces,
                value => options.DeleteInvalidSpaces = value);

            AddAction(panel, "Delete unused view templates",
                "Deletes view templates that are not assigned to any non-template view.",
                options.DeleteUnusedViewTemplates,
                value => options.DeleteUnusedViewTemplates = value);

            AddAction(panel, "Delete unused filters",
                "Deletes parameter filters that are not applied to any eligible view.",
                options.DeleteUnusedFilters,
                value => options.DeleteUnusedFilters = value);

            AddAction(panel, "Delete unused text styles",
                "Deletes text note types not used by placed text notes, while skipping internal-looking types.",
                options.DeleteUnusedTextStyles,
                value => options.DeleteUnusedTextStyles = value);

            AddAction(panel, "Fix duplicate Mark warnings",
                "Renames duplicate Mark values by adding suffixes such as _A, _B and _C.",
                options.FixDuplicateMarks,
                value => options.FixDuplicateMarks = value);

            AddAction(panel, "Delete safe duplicate instances",
                "Uses warning data and safety checks to keep the best candidate and delete duplicate elements.",
                options.DeleteDuplicateInstances,
                value => options.DeleteDuplicateInstances = value);

            AddAction(panel, "Create Naviswork export 3D view if missing",
                "Creates a 3D view named Naviswork Export when no Naviswork view is found.",
                options.EnsureNavisworkView,
                value => options.EnsureNavisworkView = value);

            ScrollViewer scrollViewer = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 500,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            return new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 219)),
                Background = Brushes.White,
                Padding = new Thickness(12),
                Child = scrollViewer
            };
        }

        private void AddAction(
            Panel panel,
            string title,
            string description,
            bool isChecked,
            Action<bool> setter)
        {
            CheckBox checkBox = new CheckBox
            {
                IsChecked = isChecked,
                Margin = new Thickness(0, 0, 0, 3),
                Content = title,
                FontWeight = FontWeights.SemiBold
            };

            checkBox.Checked += (sender, args) =>
            {
                setter(true);
                UpdateRunButtonState();
            };

            checkBox.Unchecked += (sender, args) =>
            {
                setter(false);
                UpdateRunButtonState();
            };

            TextBlock descriptionText = new TextBlock
            {
                Text = description,
                Margin = new Thickness(22, 0, 0, 9),
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128))
            };

            panel.Children.Add(checkBox);
            panel.Children.Add(descriptionText);
            actionCheckBoxes.Add(checkBox);
        }

        private StackPanel CreateButtonPanel()
        {
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };

            Button selectAllButton = new Button
            {
                Content = "Select All",
                MinWidth = 92,
                Margin = new Thickness(0, 0, 8, 0)
            };
            selectAllButton.Click += (sender, args) => SetAllActions(true);

            Button clearButton = new Button
            {
                Content = "Clear",
                MinWidth = 78,
                Margin = new Thickness(0, 0, 8, 0)
            };
            clearButton.Click += (sender, args) => SetAllActions(false);

            Button cancelButton = new Button
            {
                Content = "Cancel",
                MinWidth = 82,
                Margin = new Thickness(0, 0, 8, 0),
                IsCancel = true
            };
            cancelButton.Click += (sender, args) =>
            {
                DialogResult = false;
                Close();
            };

            runButton = new Button
            {
                Content = "Run Selected Fixes",
                MinWidth = 140,
                IsDefault = true
            };
            runButton.Click += (sender, args) =>
            {
                DialogResult = true;
                Close();
            };

            buttonPanel.Children.Add(selectAllButton);
            buttonPanel.Children.Add(clearButton);
            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(runButton);

            return buttonPanel;
        }

        private void SetAllActions(bool isChecked)
        {
            foreach (CheckBox checkBox in actionCheckBoxes)
            {
                checkBox.IsChecked = isChecked;
            }

            UpdateRunButtonState();
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateRunButtonState();
        }

        private void UpdateRunButtonState()
        {
            if (runButton == null)
            {
                return;
            }

            bool confirmed = confirmationCheckBox != null && confirmationCheckBox.IsChecked == true;
            runButton.IsEnabled = confirmed && options.HasAnySelectedAction();
        }
    }
}
