using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication_API.DTO;

public class YearRangeAttribute:RangeAttribute
{
    public YearRangeAttribute() : base(1000, DateTime.Now.Year){}// limit the maximum year is the current year
}
