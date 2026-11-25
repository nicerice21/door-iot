namespace DoorMonitor.Core.Doors;

/// <summary>1 日分の利用状況。Date は指定したタイムゾーンでの日付。</summary>
public sealed record DailyUsage(DateOnly Date, int OpenCount, long OpenSeconds, int MaxOpenSeconds);

/// <summary>
/// 開閉履歴から集計した利用状況。日別と時間帯別（0〜23時）に分けて返す。
/// </summary>
public sealed record DoorUsageStatistics(
    int OpenCount,
    int CloseCount,
    long TotalOpenSeconds,
    int AverageOpenSeconds,
    int MaxOpenSeconds,
    IReadOnlyList<DailyUsage> Daily,
    IReadOnlyList<int> OpenCountByHour)
{
    /// <summary>
    /// 履歴を日付・時間帯へ集計する。日付の境界は offset（UTC からの差）で決める。
    /// 開放時間は「閉じたイベントの changedAt - openedAt」で計算し、閉じていない分は含めない。
    /// </summary>
    public static DoorUsageStatistics Build(
        IReadOnlyList<DoorEvent> events,
        DateOnly fromDate,
        DateOnly toDate,
        TimeSpan offset)
    {
        ArgumentNullException.ThrowIfNull(events);

        var daily = new SortedDictionary<DateOnly, (int Opens, long Seconds, int Max)>();
        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            daily[date] = (0, 0, 0);
        }

        var byHour = new int[24];
        var openCount = 0;
        var closeCount = 0;
        long totalSeconds = 0;
        var maxSeconds = 0;

        foreach (var doorEvent in events)
        {
            var local = doorEvent.ChangedAt.ToOffset(offset);
            var date = DateOnly.FromDateTime(local.DateTime);
            if (!daily.ContainsKey(date))
            {
                continue;
            }

            if (doorEvent.State == DoorState.Open)
            {
                openCount++;
                byHour[local.Hour]++;
                var current = daily[date];
                daily[date] = (current.Opens + 1, current.Seconds, current.Max);
                continue;
            }

            closeCount++;
            if (doorEvent.OpenedAt is not { } openedAt)
            {
                continue;
            }

            var seconds = ToSeconds(doorEvent.ChangedAt - openedAt);
            totalSeconds += seconds;
            maxSeconds = Math.Max(maxSeconds, seconds);

            var day = daily[date];
            daily[date] = (day.Opens, day.Seconds + seconds, Math.Max(day.Max, seconds));
        }

        var average = closeCount == 0 ? 0 : (int)Math.Round((double)totalSeconds / closeCount);

        return new DoorUsageStatistics(
            openCount,
            closeCount,
            totalSeconds,
            average,
            maxSeconds,
            daily.Select(pair => new DailyUsage(pair.Key, pair.Value.Opens, pair.Value.Seconds, pair.Value.Max)).ToArray(),
            byHour);
    }

    private static int ToSeconds(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return 0;
        }

        return (int)Math.Min(int.MaxValue, Math.Round(duration.TotalSeconds));
    }
}
