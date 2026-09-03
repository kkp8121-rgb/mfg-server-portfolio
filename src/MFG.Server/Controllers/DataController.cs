using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MFG.Server.Configuration;
using MFG.Server.DTOs;

namespace MFG.Server.Controllers;

/// <summary>
/// 3-Tier 데이터 버전/Config 전달. Phase 26 Sprint 26-1 S261-01/02.
/// 관련: server/Docs/Planning/data-pipeline.md
/// </summary>
[ApiController]
[Route("api/v1/data")]
public class DataController : ControllerBase
{
    private readonly IOptionsSnapshot<DataVersionsConfig> _versions;

    public DataController(IOptionsSnapshot<DataVersionsConfig> versions)
    {
        _versions = versions;
    }

    /// <summary>현재 데이터 버전 해시 반환. 인증 불필요 — 부팅 직후 호출용.</summary>
    [HttpGet("version")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<DataVersionResponse>> GetVersion()
    {
        // IOptionsSnapshot 은 요청마다 재평가 → appsettings 수정 시 재기동 없이 반영
        var v = _versions.Value;
        return Ok(ApiResponse<DataVersionResponse>.Ok(new DataVersionResponse
        {
            Visual = v.Visual,
            Config = v.Config,
            Balance = v.Balance,
            Catalog = v.Catalog,
            ServerTime = DateTime.UtcNow
        }));
    }

    /// <summary>
    /// Config 타입별 최신 CDN URL 목록. 인증 불필요.
    /// ?type=... 지정 시 해당 타입만 필터, 생략 시 전체 반환.
    /// S261-02.
    /// </summary>
    [HttpGet("config/latest")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<DataConfigLatestResponse>> GetConfigLatest([FromQuery] string? type = null)
    {
        var v = _versions.Value;
        var baseUrl = (v.ConfigCdnBaseUrl ?? string.Empty).TrimEnd('/');
        var configTypes = v.ConfigTypes ?? new Dictionary<string, string>();

        IEnumerable<KeyValuePair<string, string>> source = configTypes;
        if (!string.IsNullOrWhiteSpace(type))
        {
            source = source.Where(kv => string.Equals(kv.Key, type, StringComparison.OrdinalIgnoreCase));
        }

        var entries = source
            .Select(kv => new DataConfigEntry
            {
                Type = kv.Key,
                Hash = kv.Value,
                // 해시 기반 파일명으로 CDN 영구 캐시 가능 (버전 변경 = 새 URL)
                Url = $"{baseUrl}/{kv.Key}-{kv.Value}.json"
            })
            .ToList();

        return Ok(ApiResponse<DataConfigLatestResponse>.Ok(new DataConfigLatestResponse
        {
            Entries = entries,
            ServerTime = DateTime.UtcNow
        }));
    }
}
