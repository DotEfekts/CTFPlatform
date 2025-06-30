using ZNetCS.AspNetCore.Logging.EntityFrameworkCore;

namespace CTFPlatform.Models;

public class AppLog : Log<int>
{
    public DateTime TimeStampSqlite { get; set; }
}