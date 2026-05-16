using System.ComponentModel.DataAnnotations;

namespace Project_SharedClassLibrary.Validation;

public sealed class YearRangeAttribute : RangeAttribute
{
    public YearRangeAttribute()
        : base(1000, DateTime.Now.Year)
    {
    }
}
