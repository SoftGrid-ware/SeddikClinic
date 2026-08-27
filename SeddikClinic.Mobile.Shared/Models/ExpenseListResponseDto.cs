using System.Text.Json.Serialization;
using SeddikClinic.Core.DTOs.Financial;

namespace SeddikClinic.Mobile.Shared.Models;

public class ExpenseListResponseDto
{
    [JsonPropertyName("items")]
    public List<ExpenseDto> Items { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}
