using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Contexts;
using Kreta.Core;

public class ExcursionPaymentTrackingView : IEvolView
{
    private const string ExcursionEntityType = "Excursion";
    private const string PaymentEntityType = "ExcursionPayment";
    private readonly ITeacherContext? _context;

    private List<GenericRecord>? _allExcursions;
    private List<GenericRecord>? _allPayments;
    private TextBox? _amountTextBox;
    private List<User>? _classStudents;
    private StackPanel? _detailsPanel;

    private ComboBox? _excursionComboBox;
    private TextBlock? _paymentInfoTextBlock;
    private Button? _saveButton;
    private GenericRecord? _selectedExcursion;
    private User? _selectedStudent;
    private TextBlock? _statusTextBlock;
    private ListBox? _studentListBox;
    private TextBlock? _studentNameTextBlock;

    public ExcursionPaymentTrackingView()
    {
    }

    public ExcursionPaymentTrackingView(ITeacherContext context)
    {
        _context = context;
    }

    public new string Name => "Kirándulás Befizetések";
    public string Description => "Kirándulásokhoz tartozó diák befizetések követése.";

    public Control CreateView()
    {
        var mainPanel = new DockPanel();

        var topPanel = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(10) };
        topPanel.Children.Add(new TextBlock { Text = "Kirándulás:", VerticalAlignment = VerticalAlignment.Center });
        _excursionComboBox = new ComboBox { Width = 250 };
        _excursionComboBox.SelectionChanged += OnExcursionSelectionChanged;
        topPanel.Children.Add(_excursionComboBox);
        DockPanel.SetDock(topPanel, Dock.Top);

        _detailsPanel = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(10),
            Width = 300,
            IsVisible = false
        };
        DockPanel.SetDock(_detailsPanel, Dock.Right);

        _studentNameTextBlock = new TextBlock { FontWeight = FontWeight.Bold };
        _paymentInfoTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var amountPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        amountPanel.Children.Add(new TextBlock
            { Text = "Új befizetés:", VerticalAlignment = VerticalAlignment.Center });
        _amountTextBox = new TextBox { Width = 100 };
        amountPanel.Children.Add(_amountTextBox);
        _saveButton = new Button { Content = "Mentés" };
        _saveButton.Click += OnSaveButtonClick;

        _detailsPanel.Children.Add(_studentNameTextBlock);
        _detailsPanel.Children.Add(new Separator());
        _detailsPanel.Children.Add(_paymentInfoTextBlock);
        _detailsPanel.Children.Add(amountPanel);
        _detailsPanel.Children.Add(_saveButton);

        _statusTextBlock = new TextBlock { Margin = new Thickness(10), Height = 20 };
        DockPanel.SetDock(_statusTextBlock, Dock.Bottom);

        _studentListBox = new ListBox { Margin = new Thickness(10, 0, 10, 10) };
        _studentListBox.SelectionChanged += OnStudentSelectionChanged;

        mainPanel.Children.Add(topPanel);
        mainPanel.Children.Add(_statusTextBlock);
        mainPanel.Children.Add(_detailsPanel);
        mainPanel.Children.Add(_studentListBox);

        LoadInitialData();

        return mainPanel;
    }

    private void LoadInitialData()
    {
        if (_context == null)
        {
            SetStatus("Hiba: A kontextus nincs beállítva.", true);
            return;
        }

        try
        {
            _allExcursions = _context.QueryEntities(ExcursionEntityType);
            if (_excursionComboBox != null)
                _excursionComboBox.ItemsSource =
                    _allExcursions.Select(e => e.Data.GetValueOrDefault("Name", "Névtelen")).ToList();

            _classStudents = _context.GetMyClassStudents();
            if (_studentListBox != null) _studentListBox.ItemsSource = _classStudents.Select(s => s.Name).ToList();
            SetStatus("Adatok betöltve.");
        }
        catch (Exception ex)
        {
            SetStatus($"Hiba az adatok betöltésekor: {ex.Message}", true);
        }
    }

    private void OnExcursionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_excursionComboBox == null || _excursionComboBox.SelectedIndex == -1 || _allExcursions == null) return;

        _selectedExcursion = _allExcursions[_excursionComboBox.SelectedIndex];
        _allPayments = _context?.QueryEntities(PaymentEntityType)
            .Where(p => p.Data.GetValueOrDefault("ExcursionId") == _selectedExcursion.Id.ToString())
            .ToList();

        UpdateDetailsPanel();
        SetStatus($"Kiválasztott kirándulás: {_selectedExcursion.Data.GetValueOrDefault("Name")}");
    }

    private void OnStudentSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_studentListBox == null || _studentListBox.SelectedIndex == -1 || _classStudents == null)
        {
            if (_detailsPanel != null) _detailsPanel.IsVisible = false;
            return;
        }

        _selectedStudent = _classStudents[_studentListBox.SelectedIndex];
        UpdateDetailsPanel();
    }

    private void UpdateDetailsPanel()
    {
        if (_selectedStudent == null || _selectedExcursion == null || _detailsPanel == null)
        {
            if (_detailsPanel != null) _detailsPanel.IsVisible = false;
            return;
        }

        _detailsPanel.IsVisible = true;

        if (_studentNameTextBlock != null) _studentNameTextBlock.Text = _selectedStudent.Name;

        if (_paymentInfoTextBlock != null && _amountTextBox != null)
        {
            _amountTextBox.Text = string.Empty;

            var paymentRecord = _allPayments?.FirstOrDefault(p =>
                p.Data.GetValueOrDefault("StudentId") == _selectedStudent.Id.ToString());
            decimal.TryParse(_selectedExcursion.Data.GetValueOrDefault("TotalCost", "0"), NumberStyles.Any,
                CultureInfo.InvariantCulture, out var totalCost);
            decimal.TryParse(paymentRecord?.Data.GetValueOrDefault("AmountPaid", "0"), NumberStyles.Any,
                CultureInfo.InvariantCulture, out var amountPaid);

            var remaining = totalCost - amountPaid;
            _paymentInfoTextBlock.Text = $"Teljes költség: {totalCost:C}\n" +
                                         $"Befizetve: {amountPaid:C}\n" +
                                         $"Hátralék: {remaining:C}";

            if (remaining <= 0)
                _paymentInfoTextBlock.Foreground = Brushes.Green;
            else
                _paymentInfoTextBlock.Foreground = Brushes.OrangeRed;
        }
    }

    private void OnSaveButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _selectedExcursion == null || _selectedStudent == null || _amountTextBox == null)
        {
            SetStatus("Nincs kiválasztva kirándulás vagy diák.", true);
            return;
        }

        if (!decimal.TryParse(_amountTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture,
                out var newPaymentAmount) || newPaymentAmount <= 0)
        {
            SetStatus("Érvénytelen összeg.", true);
            return;
        }

        try
        {
            var paymentRecord = _allPayments?.FirstOrDefault(p =>
                p.Data.GetValueOrDefault("StudentId") == _selectedStudent.Id.ToString());

            decimal.TryParse(paymentRecord?.Data.GetValueOrDefault("AmountPaid", "0"), NumberStyles.Any,
                CultureInfo.InvariantCulture, out var currentAmountPaid);
            var totalPaid = currentAmountPaid + newPaymentAmount;

            var data = new Dictionary<string, string>
            {
                { "ExcursionId", _selectedExcursion.Id.ToString() },
                { "StudentId", _selectedStudent.Id.ToString() },
                { "StudentName", _selectedStudent.Name },
                { "AmountPaid", totalPaid.ToString(CultureInfo.InvariantCulture) }
            };

            var recordId = paymentRecord?.Id ?? 0;
            _context.SaveEntity(PaymentEntityType, data, recordId == 0 ? null : recordId);

            SetStatus("Befizetés sikeresen rögzítve.");

            _allPayments = _context.QueryEntities(PaymentEntityType)
                .Where(p => p.Data.GetValueOrDefault("ExcursionId") == _selectedExcursion.Id.ToString())
                .ToList();
            UpdateDetailsPanel();
        }
        catch (Exception ex)
        {
            SetStatus($"Hiba mentés közben: {ex.Message}", true);
        }
    }

    private void SetStatus(string message, bool isError = false)
    {
        if (_statusTextBlock == null) return;
        _statusTextBlock.Text = message;
        _statusTextBlock.Foreground = isError ? Brushes.Red : Brushes.Green;
    }
}