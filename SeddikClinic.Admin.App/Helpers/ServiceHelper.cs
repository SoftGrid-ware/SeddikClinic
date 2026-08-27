namespace SeddikClinic.Admin.App.Helpers;

public static class ServiceHelper
{
    public static T GetService<T>() where T : class
    {
        try
        {
            var service = IPlatformApplication.Current?.Services.GetService<T>();
            if (service != null) return service;
        }
        catch { }

        return Activator.CreateInstance<T>();
    }
}
