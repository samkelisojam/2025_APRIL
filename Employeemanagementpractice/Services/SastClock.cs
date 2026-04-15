namespace Employeemanagementpractice.Services
{
    /// <summary>
    /// South African Standard Time (SAST) helper - UTC+2, no daylight saving.
    /// Use SastClock.Now / SastClock.Today instead of DateTime.Now / DateTime.Today.
    /// </summary>
    public static class SastClock
    {
        private static readonly TimeZoneInfo SastZone =
            TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows()
                    ? "South Africa Standard Time"
                    : "Africa/Johannesburg");

        /// <summary>Current date and time in SAST (UTC+2).</summary>
        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SastZone);

        /// <summary>Today's date in SAST (UTC+2), time at midnight.</summary>
        public static DateTime Today => Now.Date;

        /// <summary>Format a nullable DateTime to SAST 24-hour string (HH:mm).</summary>
        public static string? Format24(DateTime? dt, string fmt = "HH:mm")
            => dt.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(
                   dt.Value.Kind == DateTimeKind.Utc ? dt.Value : dt.Value.ToUniversalTime(), SastZone).ToString(fmt) : null;

        /// <summary>Format a DateTime to SAST 24-hour with seconds (HH:mm:ss).</summary>
        public static string Format24s(DateTime dt)
            => TimeZoneInfo.ConvertTimeFromUtc(
                   dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime(), SastZone).ToString("HH:mm:ss");
    }
}
