using System.Globalization;

namespace SeddikClinic.Admin.App.Converters;

public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value as string);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var str = value?.ToString();
        return str switch
        {
            "Completed" => Color.FromArgb("#10B981"),
            "Confirmed" => Color.FromArgb("#0EA5E9"),
            "Scheduled" => Color.FromArgb("#3B82F6"),
            "Waiting" => Color.FromArgb("#F59E0B"),
            "InProgress" => Color.FromArgb("#8B5CF6"),
            "Cancelled" => Color.FromArgb("#EF4444"),
            _ => Color.FromArgb("#64748B")
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StatusTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var str = value?.ToString();
        return str switch
        {
            "Completed" => "تم الكشف ✅",
            "Confirmed" => "مؤكد 👍",
            "Scheduled" => "مجدول 📅",
            "Waiting" => "في الانتظار ⏳",
            "InProgress" => "داخل العيادة 🩺",
            "Cancelled" => "ملغي ❌",
            _ => str ?? ""
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
