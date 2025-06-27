using Microsoft.EntityFrameworkCore;

namespace CTFPlatform.Models;

[PrimaryKey(nameof(Key))]
public class Setting
{
    public string Key { get; set; }
    public string Value { get; set; }
}