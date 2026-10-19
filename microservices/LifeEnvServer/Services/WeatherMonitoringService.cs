using LifeEnvServer.Data;
using LifeEnvServer.Models;
using Microsoft.EntityFrameworkCore;

namespace LifeEnvServer.Services;

/// <summary>
/// 날씨 모니터링 및 기준 체크 서비스 구현체
/// </summary>
public class WeatherMonitoringService : IWeatherMonitoringService
{
    private readonly LifeEnvDbContext _db;
    private readonly ILogger<WeatherMonitoringService> _logger;
    private readonly WeatherNotifyClient _notify;

    public WeatherMonitoringService(LifeEnvDbContext db, ILogger<WeatherMonitoringService> logger, WeatherNotifyClient notify)
    {
        _db = db;
        _logger = logger;
        _notify = notify;
    }

    /// <summary>수집된 날씨가 설정된 기준을 넘으면 기록하고 알린다.</summary>
    public async Task CheckWeatherStandardsAsync(WeatherInfo weatherInfo)
    {
        var standards = await _db.WeatherStandards
            .Where(s => !s.IsDeleted)
            .ToListAsync();

        foreach (var std in standards)
        {
            double? actualValue = GetValueByCategory(weatherInfo, std);
            if (actualValue == null) continue;

            // 1. 기본 조건 체크 (절대값 기준)
            bool isMet = CheckCondition(actualValue.Value, std);

            // 2. 복합 조건 체크 (한파 - 전일 대비 하강폭)
            if (isMet && std.Category.ToUpper() == "COLD" && std.PrevDayDiff.HasValue)
            {
                // 24시간 전 동일 위치의 기온 조회
                var yesterday = weatherInfo.ObservationTime.AddDays(-1);
                var yesterdayInfo = await _db.WeatherInfos
                    .Where(w => w.WeatherLocationId == weatherInfo.WeatherLocationId &&
                                w.ObservationTime <= yesterday)
                    .OrderByDescending(w => w.ObservationTime)
                    .FirstOrDefaultAsync();

                if (yesterdayInfo != null)
                {
                    // 체감온도 사용 여부에 따라 비교 대상 온도 결정
                    double currentTemp = std.UseSensibleTemp ? (weatherInfo.SensibleTemp ?? weatherInfo.TemperatureC) : weatherInfo.TemperatureC;
                    double pastTemp = std.UseSensibleTemp ? (yesterdayInfo.SensibleTemp ?? yesterdayInfo.TemperatureC) : yesterdayInfo.TemperatureC;

                    double dropAmount = pastTemp - currentTemp;

                    // 설정된 하강폭(PrevDayDiff)보다 적게 떨어졌다면 조건 미달
                    if (dropAmount < std.PrevDayDiff.Value)
                    {
                        isMet = false;
                    }
                }
                else
                {
                    isMet = false;
                }
            }

            if (isMet)
            {
                // 알림 주기(분) 계산: 설정값이 있으면 사용, 없으면 기본 10분
                int intervalMinutes = std.NotificationInterval ?? 10;
                var lastNotifiedTimeLimit = DateTimeOffset.UtcNow.AddMinutes(-intervalMinutes);

                // 중복 기록 체크 (설정된 주기 내 동일 위치, 동일 기준에 대해 기록이 남은 적이 있는지 확인)
                bool isRecentlyNotified = await _db.WeatherEventRecords
                    .AnyAsync(r => r.WeatherStandardId == std.Id &&
                                   r.WeatherInfo!.Location == weatherInfo.Location &&
                                   r.IsNotified == true &&
                                   r.EventTime >= lastNotifiedTimeLimit);

                if (!isRecentlyNotified)
                {
                    var record = new WeatherEventRecord
                    {
                        WeatherInfoId = weatherInfo.Id,
                        WeatherStandardId = std.Id,
                        EventTime = DateTimeOffset.UtcNow,
                        MeasuredValue = actualValue.Value,
                        // IsNotified 는 기준 충족을 기록했다는 뜻이지 발송 성공이 아니다. 중복 억제가 이 값에 걸려 있어 성공일 때만 세우면 장애 뒤 재발송이 몰린다.
                        IsNotified = true,
                        CreatedAt = DateTimeOffset.UtcNow,
                        CreatedBy = "WeatherMonitoring"
                    };

                    _db.WeatherEventRecords.Add(record);
                    await _db.SaveChangesAsync();

                    _logger.LogInformation("기상 기준 충족 기록: {StandardName} ({Location}) 현재값 {Value}{Unit}",
                        std.Name, weatherInfo.Location, actualValue.Value, std.Unit);

                    // 알림 발송 (D-G1a) — NotificationServer 가 날씨 스위치를 켠 구독자에게 보낸다.
                    // 실패해도 판정·기록은 이미 끝났고, 다음 주기 판정에도 영향이 없다.
                    await _notify.NotifyAsync(
                        std.Name, weatherInfo.Location ?? string.Empty, std.Category,
                        actualValue.Value, std.Unit ?? string.Empty, record.EventTime);
                }
            }
        }
    }

    private double? GetValueByCategory(WeatherInfo info, WeatherStandard std)
    {
        string category = std.Category.ToUpper();

        // 기온 관련 카테고리이면서 체감온도 사용이 설정된 경우
        if (std.UseSensibleTemp && (category == "HEAT" || category == "COLD" || category == "T1H"))
        {
            return info.SensibleTemp ?? info.TemperatureC; // 계산된 체감온도가 없으면 실제기온 사용
        }

        return category switch
        {
            "WIND" => info.WindSpeed,
            "RAIN" => info.Rainfall,
            "SNOW" => info.Snowfall,
            "HEAT" => info.TemperatureC,
            "COLD" => info.TemperatureC,
            "T1H" => info.TemperatureC,
            "REH" => (double?)info.Humidity,
            "WSD" => info.WindSpeed,
            "RN1" => info.Rainfall,
            _ => null
        };
    }

    private bool CheckCondition(double actual, WeatherStandard std)
    {
        if (std.ThresholdValue == null) return false;

        double v1 = std.ThresholdValue.Value;
        double? v2 = std.ThresholdValue2;

        return std.Operator?.ToUpper() switch
        {
            "GE" => actual >= v1,
            "LE" => actual <= v1,
            "GT" => actual > v1,
            "LT" => actual < v1,
            "EQ" => Math.Abs(actual - v1) < 0.001,
            "BT" => v2.HasValue && actual >= Math.Min(v1, v2.Value) && actual <= Math.Max(v1, v2.Value),
            "NB" => v2.HasValue && (actual < Math.Min(v1, v2.Value) || actual > Math.Max(v1, v2.Value)),
            "DGE" => v2.HasValue && Math.Abs(actual - v1) >= v2.Value,
            "DLE" => v2.HasValue && Math.Abs(actual - v1) <= v2.Value,
            _ => actual >= v1 // Default to GE for backward compatibility
        };
    }
}
