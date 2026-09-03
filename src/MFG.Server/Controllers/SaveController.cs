using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MFG.Data;
using MFG.Domain.Entities;
using MFG.Server.DTOs;

namespace MFG.Server.Controllers;

[ApiController]
[Route("api/v1/save")]
[Authorize]
public class SaveController : ControllerBase
{
    private readonly AppDbContext _db;

    public SaveController(AppDbContext db)
    {
        _db = db;
    }

    private const int MAX_SAVE_SIZE = 5 * 1024 * 1024; // 5MB
    private const int MIN_SYNC_INTERVAL_SECONDS = 30;

    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] SaveSyncRequest request, CancellationToken ct)
    {
        var playerId = await GetPlayerIdAsync(ct);
        if (playerId is null)
            return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        // 사이즈 제한
        if (request.SaveData.Length > MAX_SAVE_SIZE)
            return BadRequest(ApiResponse<object>.Fail($"세이브 데이터가 너무 큽니다: {request.SaveData.Length / 1024}KB (최대 5MB)"));

        // 클라이언트 타임스탬프 검증 (미래 시간 차단)
        if (request.ClientTimestamp > DateTime.UtcNow.AddMinutes(5))
            return BadRequest(ApiResponse<object>.Fail("클라이언트 타임스탬프가 서버 시간보다 미래입니다."));

        var progress = await _db.ProgressData
            .FirstOrDefaultAsync(d => d.PlayerId == playerId.Value, ct);

        if (progress is null)
        {
            progress = new ProgressData
            {
                PlayerId = playerId.Value,
                SaveJson = request.SaveData,
                Version = 1
            };
            _db.ProgressData.Add(progress);
        }
        else
        {
            // 쓰로틀: 마지막 저장으로부터 최소 30초
            var elapsed = (DateTime.UtcNow - progress.UpdatedAt).TotalSeconds;
            if (elapsed < MIN_SYNC_INTERVAL_SECONDS)
                return BadRequest(ApiResponse<object>.Fail($"저장 간격이 너무 짧습니다. {MIN_SYNC_INTERVAL_SECONDS - (int)elapsed}초 후 시도하세요."));

            progress.SaveJson = request.SaveData;
            progress.Version++;
        }

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<SaveSyncResponse>.Ok(new SaveSyncResponse
        {
            PlayerId = playerId.Value,
            VersionUpdated = progress.Version,
            ServerTime = DateTime.UtcNow,
            NextSyncWindow = 300
        }));
    }

    // 최초 로그인 시 로컬 SaveData.json → 서버 1회 업로드 (S251-01).
    // 기존 ProgressData가 없을 때만 수락. 성공 시 migrated=true, 이미 존재하면 migrated=false + reason="already_exists".
    [HttpPost("migrate")]
    public async Task<IActionResult> Migrate([FromBody] SaveMigrateRequest request, CancellationToken ct)
    {
        var playerId = await GetPlayerIdAsync(ct);
        if (playerId is null)
            return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        if (request.SaveData.Length > MAX_SAVE_SIZE)
            return BadRequest(ApiResponse<object>.Fail($"세이브 데이터가 너무 큽니다: {request.SaveData.Length / 1024}KB (최대 5MB)"));

        if (request.ClientTimestamp > DateTime.UtcNow.AddMinutes(5))
            return BadRequest(ApiResponse<object>.Fail("클라이언트 타임스탬프가 서버 시간보다 미래입니다."));

        var progress = await _db.ProgressData
            .FirstOrDefaultAsync(d => d.PlayerId == playerId.Value, ct);

        if (progress is not null)
        {
            // 이미 서버에 데이터 있음 → 마이그레이션 거부 (클라는 /save/load로 서버 데이터 내려받기)
            return Ok(ApiResponse<SaveMigrateResponse>.Ok(new SaveMigrateResponse
            {
                PlayerId = playerId.Value,
                Migrated = false,
                Version = progress.Version,
                ServerTime = DateTime.UtcNow,
                Reason = "already_exists"
            }));
        }

        progress = new ProgressData
        {
            PlayerId = playerId.Value,
            SaveJson = request.SaveData,
            Version = 1
        };
        _db.ProgressData.Add(progress);
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<SaveMigrateResponse>.Ok(new SaveMigrateResponse
        {
            PlayerId = playerId.Value,
            Migrated = true,
            Version = progress.Version,
            ServerTime = DateTime.UtcNow,
            Reason = null
        }));
    }

    [HttpGet("load")]
    public async Task<IActionResult> Load(CancellationToken ct)
    {
        var playerId = await GetPlayerIdAsync(ct);
        if (playerId is null)
            return Unauthorized(ApiResponse<object>.Fail("인증 실패"));

        var progress = await _db.ProgressData
            .FirstOrDefaultAsync(d => d.PlayerId == playerId.Value, ct);

        return Ok(ApiResponse<SaveLoadResponse>.Ok(new SaveLoadResponse
        {
            PlayerId = playerId.Value,
            SaveData = progress?.SaveJson ?? "{}",
            Version = progress?.Version ?? 0,
            LastUpdatedAt = progress?.UpdatedAt ?? DateTime.UtcNow,
            ServerTime = DateTime.UtcNow
        }));
    }

    private async Task<long?> GetPlayerIdAsync(CancellationToken ct)
    {
        var firebaseUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("user_id");
        if (string.IsNullOrEmpty(firebaseUid)) return null;

        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.FirebaseUid == firebaseUid, ct);
        return player?.Id;
    }
}
