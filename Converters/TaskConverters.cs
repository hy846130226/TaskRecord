using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TaskRecorder.Services;
using TaskRecorder.ViewModels;

namespace TaskRecorder.Converters;

public class TimeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not FlatNodeInfo nodeInfo)
            return "";

        var task = nodeInfo.Task;
        var createdStr = task.CreatedAt.ToString("MM-dd");
        var milestoneStr = "";

        if (task.MilestoneDays.HasValue)
        {
            var milestoneDate = WorkdayCalculator.AddWorkdays(task.CreatedAt, task.MilestoneDays.Value);
            milestoneStr = $"  ⏱ {milestoneDate:MM-dd}";
        }

        if (task.IsCompleted && task.CompletedAt.HasValue)
        {
            var completedStr = task.CompletedAt.Value.ToString("MM-dd");
            return $"{createdStr}{milestoneStr}  ✓ {completedStr}";
        }

        return $"{createdStr}{milestoneStr}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class MilestoneOverdueConverter : IValueConverter
{
    private static readonly Brush DefaultBrush = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
    private static readonly Brush OverdueBrush = Brushes.Red;

    static MilestoneOverdueConverter()
    {
        DefaultBrush.Freeze();
        OverdueBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not FlatNodeInfo nodeInfo)
            return DefaultBrush;

        var task = nodeInfo.Task;
        if (task.IsCompleted || !task.MilestoneDays.HasValue)
            return DefaultBrush;

        var milestoneDate = WorkdayCalculator.AddWorkdays(task.CreatedAt, task.MilestoneDays.Value);
        return DateTime.Today >= milestoneDate.Date ? OverdueBrush : DefaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class CompletedStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isCompleted && isCompleted)
            return TextDecorations.Strikethrough;
        return new TextDecorationCollection();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class ConnectorVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool hasNext && hasNext)
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class BoolInverseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool v)
            return !v;
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool v)
            return !v;
        return true;
    }
}

public class CompleteMenuHeaderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isCompleted && isCompleted)
            return "取消完成";
        return "完成";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class HasNotesConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string notes && !string.IsNullOrWhiteSpace(notes))
            return true;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class RecentTaskBackgroundConverter : IMultiValueConverter
{
    private static readonly Brush DefaultBrush = Brushes.White;
    private static readonly Brush RecentCompletedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xE0));
    private static readonly Brush RecentPendingBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xFA, 0xCD));

    static RecentTaskBackgroundConverter()
    {
        RecentCompletedBrush.Freeze();
        RecentPendingBrush.Freeze();
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 4)
            return DefaultBrush;

        var isHighlight = values[0] is bool b && b;
        if (!isHighlight)
            return DefaultBrush;

        var isCompleted = values[1] is bool c && c;
        var createdAt = values[2] is DateTime dt ? dt : DateTime.MinValue;
        var completedAt = values[3] is DateTime ct ? ct : (DateTime?)null;

        var relevantDate = completedAt ?? createdAt;
        if (relevantDate == DateTime.MinValue)
            return DefaultBrush;

        var cutoff = GetCutoffDate();
        if (relevantDate.Date >= cutoff)
            return isCompleted ? RecentCompletedBrush : RecentPendingBrush;

        return DefaultBrush;
    }

    private static DateTime GetCutoffDate()
    {
        var date = DateTime.Today.AddDays(-1);
        var workdays = 0;
        while (workdays < 4)
        {
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                workdays++;
            if (workdays < 4)
                date = date.AddDays(-1);
        }
        return date;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class CanAddSubtaskConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 1)
        {
            var isCompleted = values[0] is bool b && b;
            return !isCompleted;
        }
        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
