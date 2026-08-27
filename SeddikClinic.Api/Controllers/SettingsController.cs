using Microsoft.AspNetCore.Mvc;
using SeddikClinic.Core.DTOs.Settings;
using System.Text.Json;

namespace SeddikClinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private static readonly string SettingsFilePath = Path.Combine(AppContext.BaseDirectory, "working_hours.json");
    private static WorkingHoursConfigDto _cachedConfig = LoadConfig();

    private static WorkingHoursConfigDto LoadConfig()
    {
        try
        {
            if (System.IO.File.Exists(SettingsFilePath))
            {
                var json = System.IO.File.ReadAllText(SettingsFilePath);
                var config = JsonSerializer.Deserialize<WorkingHoursConfigDto>(json);
                if (config != null) return config;
            }
        }
        catch { }

        return new WorkingHoursConfigDto();
    }

    [HttpGet("working-hours")]
    public ActionResult<WorkingHoursConfigDto> GetWorkingHours()
    {
        return Ok(_cachedConfig);
    }

    [HttpPut("working-hours")]
    [HttpPost("working-hours")]
    public ActionResult<WorkingHoursConfigDto> UpdateWorkingHours([FromBody] WorkingHoursConfigDto dto)
    {
        if (dto == null) return BadRequest("بيانات غير صالحة");

        _cachedConfig = dto;
        try
        {
            var json = JsonSerializer.Serialize(_cachedConfig, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(SettingsFilePath, json);
        }
        catch { }

        return Ok(_cachedConfig);
    }
}
