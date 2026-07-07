using System;
using System.Collections.Generic;

namespace TaskRecorder.Services;

public static class WorkdayCalculator
{
    private static readonly HashSet<DateTime> s_holidays = new()
    {
        // 2026 元旦
        new(2026, 1, 1), new(2026, 1, 2),
        // 2026 春节
        new(2026, 1, 28), new(2026, 1, 29), new(2026, 1, 30), new(2026, 1, 31),
        new(2026, 2, 1), new(2026, 2, 2), new(2026, 2, 3),
        // 2026 清明节
        new(2026, 4, 5), new(2026, 4, 6), new(2026, 4, 7),
        // 2026 劳动节
        new(2026, 5, 1), new(2026, 5, 2), new(2026, 5, 3), new(2026, 5, 4), new(2026, 5, 5),
        // 2026 端午节
        new(2026, 6, 19), new(2026, 6, 20), new(2026, 6, 21),
        // 2026 中秋节
        new(2026, 9, 25), new(2026, 9, 26), new(2026, 9, 27),
        // 2026 国庆节
        new(2026, 10, 1), new(2026, 10, 2), new(2026, 10, 3), new(2026, 10, 4),
        new(2026, 10, 5), new(2026, 10, 6), new(2026, 10, 7),
    };

    /// <summary>
    /// 从起始日期开始，计算第 N 个工作日后的日期（跳过周末和节假日）。
    /// 起始日本身不计入天数。
    /// </summary>
    public static DateTime AddWorkdays(DateTime startDate, int workdays)
    {
        var date = startDate.Date;
        var count = 0;
        while (count < workdays)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
                && !s_holidays.Contains(date))
                count++;
        }
        return date;
    }
}
