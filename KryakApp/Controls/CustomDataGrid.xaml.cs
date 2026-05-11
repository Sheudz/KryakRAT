using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace KryakApp.Controls
{
    public sealed class CustomDataGridRowRightClickEventArgs : EventArgs
    {
        public CustomDataGridRowRightClickEventArgs(object rowItem)
        {
            RowItem = rowItem;
        }

        public object RowItem { get; }
    }

    public sealed class DataGridColumnDefinition
    {
        public string Header { get; set; } = string.Empty;

        public string PropertyName { get; set; } = string.Empty;

        public int WidthWeight { get; set; } = 1;

        public double MinWidth { get; set; } = 90;
    }

    public sealed partial class CustomDataGrid : UserControl
    {
        private readonly List<double> _columnWidths = new();
        private bool _isPointerDownOnHeader;
        private bool _isDraggingHeader;
        private bool _isResizing;
        private int _resizeColumnIndex = -1;
        private double _resizeStartX;
        private double _resizeStartWidth;
        private bool _hasPendingResizeVisualUpdate;
        private bool _isRenderingHooked;
        private int _dragColumnIndex = -1;
        private DataGridColumnDefinition? _dragColumn;
        private double _pressedHeaderX;
        private readonly Brush _dragColumnEdgeBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(140, 76, 163, 255));

        public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
            nameof(Columns),
            typeof(ObservableCollection<DataGridColumnDefinition>),
            typeof(CustomDataGrid),
            new PropertyMetadata(null, OnColumnsChanged));

        public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
            nameof(Items),
            typeof(ObservableCollection<object>),
            typeof(CustomDataGrid),
            new PropertyMetadata(null, OnItemsChanged));

        public CustomDataGrid()
        {
            InitializeComponent();

            Columns = new ObservableCollection<DataGridColumnDefinition>();
            Items = new ObservableCollection<object>();

            Columns.CollectionChanged += Columns_CollectionChanged;
            Items.CollectionChanged += Items_CollectionChanged;
            ContentScrollViewer.ViewChanged += ContentScrollViewer_ViewChanged;

            RenderAll();
        }

        public event EventHandler<CustomDataGridRowRightClickEventArgs>? RowRightClick;

        public ObservableCollection<DataGridColumnDefinition> Columns
        {
            get => (ObservableCollection<DataGridColumnDefinition>)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        public ObservableCollection<object> Items
        {
            get => (ObservableCollection<object>)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CustomDataGrid)d;

            if (e.OldValue is ObservableCollection<DataGridColumnDefinition> oldColumns)
            {
                oldColumns.CollectionChanged -= control.Columns_CollectionChanged;
            }

            if (e.NewValue is ObservableCollection<DataGridColumnDefinition> newColumns)
            {
                newColumns.CollectionChanged += control.Columns_CollectionChanged;
            }

            control.RenderAll();
        }

        private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CustomDataGrid)d;

            if (e.OldValue is ObservableCollection<object> oldItems)
            {
                oldItems.CollectionChanged -= control.Items_CollectionChanged;
            }

            if (e.NewValue is ObservableCollection<object> newItems)
            {
                newItems.CollectionChanged += control.Items_CollectionChanged;
            }

            control.RenderRows();
        }

        private void Columns_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RenderAll();
        }

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RenderRows();
        }

        private void RenderAll()
        {
            EnsureWidths();
            RenderHeader();
            RenderRows();
        }

        private void EnsureWidths()
        {
            if (Columns == null)
            {
                _columnWidths.Clear();
                return;
            }

            if (_columnWidths.Count > Columns.Count)
            {
                _columnWidths.RemoveRange(Columns.Count, _columnWidths.Count - Columns.Count);
            }

            while (_columnWidths.Count < Columns.Count)
            {
                int i = _columnWidths.Count;
                double min = Math.Max(50, Columns[i].MinWidth);
                double initial = Math.Max(min, Math.Max(1, Columns[i].WidthWeight) * 140);
                _columnWidths.Add(initial);
            }
        }

        private void RenderHeader()
        {
            HeaderGrid.Children.Clear();
            HeaderGrid.ColumnDefinitions.Clear();

            if (Columns == null)
            {
                return;
            }

            for (int i = 0; i < Columns.Count; i++)
            {
                double width = _columnWidths[i];

                HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(width, GridUnitType.Pixel),
                    MinWidth = Math.Max(50, Columns[i].MinWidth)
                });

                var root = new Grid { Tag = i };

                var headerBorder = new Border
                {
                    Padding = new Thickness(12, 0, 8, 0),
                    Tag = i
                };

                if (_isPointerDownOnHeader && _dragColumn != null && ReferenceEquals(Columns[i], _dragColumn))
                {
                    headerBorder.BorderBrush = _dragColumnEdgeBrush;
                    headerBorder.BorderThickness = new Thickness(2, 0, 2, 0);
                }

                var headerText = new TextBlock { Text = Columns[i].Header };
                headerText.Style = (Style)Resources["HeaderTextStyle"];
                headerBorder.Child = headerText;

                headerBorder.PointerPressed += HeaderBorder_PointerPressed;
                headerBorder.PointerMoved += HeaderBorder_PointerMoved;
                headerBorder.PointerReleased += HeaderBorder_PointerReleased;
                headerBorder.PointerCanceled += HeaderBorder_PointerReleased;

                var resizeGrip = new Border
                {
                    Width = 12,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Tag = i
                };

                resizeGrip.PointerPressed += ResizeGrip_PointerPressed;
                resizeGrip.PointerMoved += ResizeGrip_PointerMoved;
                resizeGrip.PointerReleased += ResizeGrip_PointerReleased;
                resizeGrip.PointerCanceled += ResizeGrip_PointerReleased;
                resizeGrip.PointerEntered += ResizeGrip_PointerEntered;
                resizeGrip.PointerExited += ResizeGrip_PointerExited;

                root.Children.Add(headerBorder);
                root.Children.Add(resizeGrip);

                Grid.SetColumn(root, i);
                HeaderGrid.Children.Add(root);
            }

            HeaderGrid.MinWidth = GetTotalWidth();
            HeaderTranslate.X = -ContentScrollViewer.HorizontalOffset;

        }

        private void RenderRows()
        {
            RowsPanel.Children.Clear();

            if (Items == null || Columns == null)
            {
                return;
            }

            for (int rowIndex = 0; rowIndex < Items.Count; rowIndex++)
            {
                RowsPanel.Children.Add(BuildRow(Items[rowIndex]));
            }
        }

        private Grid BuildRow(object rowItem)
        {
            var row = new Grid
            {
                MinWidth = GetTotalWidth(),
                Padding = new Thickness(0),
                BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            row.RightTapped += (sender, args) =>
            {
                RowRightClick?.Invoke(this, new CustomDataGridRowRightClickEventArgs(rowItem));
                args.Handled = true;
            };

            for (int i = 0; i < Columns.Count; i++)
            {
                row.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(_columnWidths[i], GridUnitType.Pixel),
                    MinWidth = Math.Max(50, Columns[i].MinWidth)
                });

                var cell = new Border
                {
                    Padding = new Thickness(12, 8, 8, 8)
                };

                var text = new TextBlock
                {
                    Text = ResolveCellValue(rowItem, Columns[i].PropertyName)
                };
                text.Style = (Style)Resources["CellTextStyle"];
                cell.Child = text;

                Grid.SetColumn(cell, i);
                row.Children.Add(cell);
            }

            return row;
        }

        private void HeaderBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_isResizing)
            {
                return;
            }

            if (sender is not FrameworkElement header || header.Tag is not int index)
            {
                return;
            }

            _isPointerDownOnHeader = true;
            _isDraggingHeader = false;
            _dragColumnIndex = index;
            _dragColumn = Columns[index];
            _pressedHeaderX = e.GetCurrentPoint(HeaderViewport).Position.X + ContentScrollViewer.HorizontalOffset;
            header.CapturePointer(e.Pointer);
            RenderHeader();
            e.Handled = true;
        }

        private void HeaderBorder_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                EndHeaderInteraction();
                return;
            }

            if (_isResizing || !_isPointerDownOnHeader || _dragColumnIndex < 0)
            {
                return;
            }

            double currentX = e.GetCurrentPoint(HeaderViewport).Position.X + ContentScrollViewer.HorizontalOffset;
            if (!_isDraggingHeader && Math.Abs(currentX - _pressedHeaderX) < 6)
            {
                return;
            }

            _isDraggingHeader = true;
            int insertIndex = GetInsertIndex(currentX);
            _dragColumnIndex = MoveColumnLive(_dragColumnIndex, insertIndex);
            e.Handled = true;
        }

        private void HeaderBorder_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement element)
            {
                element.ReleasePointerCapture(e.Pointer);
            }

            EndHeaderInteraction();
            e.Handled = true;
        }

        private void ResizeGrip_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not FrameworkElement grip || grip.Tag is not int index || index < 0 || index >= _columnWidths.Count)
            {
                return;
            }

            _isResizing = true;
            _resizeColumnIndex = index;
            _resizeStartX = e.GetCurrentPoint(this).Position.X;
            _resizeStartWidth = _columnWidths[index];

            _isPointerDownOnHeader = false;
            _isDraggingHeader = false;
            _dragColumnIndex = -1;
            _dragColumn = null;

            grip.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void ResizeGrip_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isResizing || _resizeColumnIndex < 0 || _resizeColumnIndex >= _columnWidths.Count)
            {
                return;
            }

            double currentX = e.GetCurrentPoint(this).Position.X;
            double delta = currentX - _resizeStartX;
            double minWidth = Math.Max(50, Columns[_resizeColumnIndex].MinWidth);

            _columnWidths[_resizeColumnIndex] = Math.Max(minWidth, _resizeStartWidth + delta);
            _hasPendingResizeVisualUpdate = true;
            EnsureResizeRenderingHook();
            e.Handled = true;
        }

        private void ResizeGrip_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isResizing = false;
            _resizeColumnIndex = -1;

            if (sender is UIElement element)
            {
                element.ReleasePointerCapture(e.Pointer);
            }

            ApplyColumnWidthsToVisuals();
            RemoveResizeRenderingHook();
            ProtectedCursor = null;
            e.Handled = true;
        }

        private void EnsureResizeRenderingHook()
        {
            if (_isRenderingHooked)
            {
                return;
            }

            CompositionTarget.Rendering += CompositionTarget_Rendering;
            _isRenderingHooked = true;
        }

        private void RemoveResizeRenderingHook()
        {
            if (!_isRenderingHooked)
            {
                return;
            }

            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            _isRenderingHooked = false;
            _hasPendingResizeVisualUpdate = false;
        }

        private void CompositionTarget_Rendering(object? sender, object e)
        {
            if (!_hasPendingResizeVisualUpdate)
            {
                if (!_isResizing)
                {
                    RemoveResizeRenderingHook();
                }

                return;
            }

            _hasPendingResizeVisualUpdate = false;
            ApplyColumnWidthsToVisuals();
        }

        private void ResizeGrip_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        }

        private void ContentScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            HeaderTranslate.X = -ContentScrollViewer.HorizontalOffset;
        }

        private void ResizeGrip_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!_isResizing)
            {
                ProtectedCursor = null;
            }
        }

        private void ApplyColumnWidthsToVisuals()
        {
            for (int i = 0; i < HeaderGrid.ColumnDefinitions.Count && i < _columnWidths.Count; i++)
            {
                HeaderGrid.ColumnDefinitions[i].Width = new GridLength(_columnWidths[i], GridUnitType.Pixel);
            }

            for (int rowIndex = 0; rowIndex < RowsPanel.Children.Count; rowIndex++)
            {
                if (RowsPanel.Children[rowIndex] is not Grid row)
                {
                    continue;
                }

                row.MinWidth = GetTotalWidth();

                for (int colIndex = 0; colIndex < row.ColumnDefinitions.Count && colIndex < _columnWidths.Count; colIndex++)
                {
                    row.ColumnDefinitions[colIndex].Width = new GridLength(_columnWidths[colIndex], GridUnitType.Pixel);
                }
            }
        }

        private void EndHeaderInteraction()
        {
            _dragColumnIndex = -1;
            _dragColumn = null;
            _isPointerDownOnHeader = false;
            _isDraggingHeader = false;
            RenderHeader();
        }

        private int GetInsertIndex(double x)
        {
            double current = 0;

            for (int i = 0; i < _columnWidths.Count; i++)
            {
                double middle = current + (_columnWidths[i] / 2);
                if (x < middle)
                {
                    return i;
                }

                current += _columnWidths[i];
            }

            return _columnWidths.Count;
        }

        private int MoveColumnLive(int sourceIndex, int insertIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= Columns.Count)
            {
                return sourceIndex;
            }

            if (insertIndex < 0)
            {
                insertIndex = 0;
            }
            else if (insertIndex > Columns.Count)
            {
                insertIndex = Columns.Count;
            }

            int targetIndex = insertIndex;
            if (targetIndex > sourceIndex)
            {
                targetIndex--;
            }

            if (targetIndex == sourceIndex)
            {
                return sourceIndex;
            }

            var movedWidth = _columnWidths[sourceIndex];
            _columnWidths.RemoveAt(sourceIndex);
            _columnWidths.Insert(targetIndex, movedWidth);

            Columns.Move(sourceIndex, targetIndex);
            RenderAll();
            return targetIndex;
        }

        private double GetTotalWidth()
        {
            double width = 0;
            for (int i = 0; i < _columnWidths.Count; i++)
            {
                width += _columnWidths[i];
            }

            return width;
        }

        private static string ResolveCellValue(object rowItem, string propertyName)
        {
            if (rowItem == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return string.Empty;
            }

            var propertyInfo = rowItem.GetType().GetProperty(propertyName);
            if (propertyInfo == null)
            {
                return string.Empty;
            }

            var value = propertyInfo.GetValue(rowItem);
            return value?.ToString() ?? string.Empty;
        }
    }
}
