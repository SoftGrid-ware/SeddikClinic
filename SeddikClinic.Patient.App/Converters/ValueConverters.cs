using System.Globalization;

namespace SeddikClinic.Patient.App.Converters;

public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value as string);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        if (value is int i) return i == 0;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToInvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && parameter is string param)
        {
            var parts = param.Split('|');
            if (parts.Length == 2)
            {
                return b ? parts[0] : parts[1];
            }
        }
        return value?.ToString() ?? string.Empty;
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
            "Confirmed" => Color.FromArgb("#0284C7"),
            "Scheduled" => Color.FromArgb("#2563EB"),
            "Waiting" => Color.FromArgb("#D97706"),
            "Cancelled" => Color.FromArgb("#DC2626"),
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
            "Completed" => "تم الكشف",
            "Confirmed" => "مؤكد",
            "Scheduled" => "مجدول",
            "Waiting" => "في صالة الانتظار",
            "InProgress" => "داخل العيادة",
            "Cancelled" => "ملغي",
            _ => str ?? ""
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
