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

    /// <summary>
    /// WeatherMonitoringService 생성자
    /// </summary>
    /// <param name="db">DB 컨텍스트</param>
    /// <param name="logger">로거</param>
    public WeatherMonitoringService(LifeEnvDbContext db, ILogger<WeatherMonitoringService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 수집된 날씨 정보를 기준으로 설정된 기준을 체크하고 부합할 경우 기록을 저장합니다.
    /// </summary>
    /// <param name="weatherInfo">수집된 날씨 정보</param>
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
                        // 발송 기능은 이식하지 않았다 — 포털 NotificationServer 연동은 결정 대기.
                        // 원본은 발송(이메일·푸시·카카오)까지 여기서 했고 IsNotified = true 로 기록했다.
                        // 중복 기록 억제(위 isRecentlyNotified 판정)가 이 값에 걸려 있어 원본대로 true 를 유지한다.
                        IsNotified = true,
                        CreatedAt = DateTimeOffset.UtcNow,
                        CreatedBy = "WeatherMonitoring"
                    };

                    _db.WeatherEventRecords.Add(record);
                    await _db.SaveChangesAsync();

                    _logger.LogInformation("기상 기준 충족 기록: {StandardName} ({Location}) 현재값 {Value}{Unit}",
                        std.Name, weatherInfo.Location, actualValue.Value, std.Unit);
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
